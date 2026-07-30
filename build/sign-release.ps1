#Requires -Version 5.1
#
# ================================ ÖNEMLİ ====================================
# BU BETİK BİLİNÇLİ OLARAK HİÇBİR YERDEN ÇAĞRILMAZ.
#
# WinOptimizer dağıtımı İMZASIZDIR (karar: docs/KURULUM.md → "SmartScreen uyarısı").
# build-installer.ps1 ve CI iş akışı imzalama adımı içermez.
#
# KENDİNDEN İMZALI (self-signed) SERTİFİKA İLE ASLA İMZALAMAYIN: kendi makinenizde
# geçerli görünür ama her hedef PC'de "geçersiz imza" olarak okunur — bu, hiç
# imzalamamaktan DAHA KÖTÜDÜR (bazı AV motorları geçersiz imzayı sinyal sayar).
#
# Gerçek bir OV/EV kod imzalama sertifikası alındığı gün altyapı hazır:
#   .\sign-release.ps1 -PublishDir <publish> -PfxPath codesign.pfx -PfxPassword $env:SIGN_PFX_PASSWORD
# ve build-installer.ps1'e publish sonrası tek bir çağrı eklemek yeterli.
# ===========================================================================
<#
.SYNOPSIS
    WinOptimizer — Release derlemesini Authenticode ile imzalar (master plan Bölüm 20.4).

.DESCRIPTION
    Publish çıktısındaki tüm exe/dll/msi dosyalarını kod imzalama sertifikasıyla imzalar,
    RFC3161 zaman damgalı, SHA-256 digest. .NET tabanlı Set-AuthenticodeSignature kullanır —
    signtool/Windows SDK gerektirmez (her Windows'ta çalışır).

    İmzalı dosyaları Get-AuthenticodeSignature ile doğrular.

.PARAMETER PublishDir
    İmzalanacak publish çıktı klasörü veya TEK bir dosya yolu (varsayılan: App publish).

.PARAMETER PfxPath
    Kod imzalama PFX dosyasının yolu. Yoksa certificate store (thumbprint) kullanılır.

.PARAMETER PfxPassword
    PFX parolası (CI'da gizli değişken: $env:SIGN_PFX_PASSWORD).

.PARAMETER Thumbprint
    Certificate store'daki sertifikanın SHA-1 parmak izi.

.PARAMETER TimestampServer
    RFC3161 zaman damgası sunucusu (varsayılan: DigiCert).

.EXAMPLE
    .\sign-release.ps1 -Thumbprint ABCDEF0123456789
    .\sign-release.ps1 -PublishDir C:\out\WinOptimizer.msi -Thumbprint ABCDEF...
#>
[CmdletBinding()]
param(
    [string]$PublishDir = (Resolve-Path (Join-Path $PSScriptRoot '..\src\WinOptimizer.App\bin\Release\net8.0-windows\publish')).Path,
    [string]$PfxPath,
    [string]$PfxPassword = $env:SIGN_PFX_PASSWORD,
    [string]$Thumbprint,
    [string]$TimestampServer = 'http://timestamp.digicert.com'
)

$ErrorActionPreference = 'Stop'

Write-Host "==> WinOptimizer imzalama (master plan Bölüm 20.4)" -ForegroundColor Cyan
Write-Host "    Hedef:     $PublishDir"
Write-Host "    Yontem:    Set-AuthenticodeSignature (signtool/SDK gerekmez)" -ForegroundColor DarkGray

# Sertifikayı yükle: PFX öncelikli, sonra thumbprint, en son otomatik kod imzalama sertifikası.
$cert = $null
if ($PfxPath -and (Test-Path $PfxPath)) {
    if (-not $PfxPassword) { throw "PFX parolasi gerekli: -PfxPassword veya env SIGN_PFX_PASSWORD" }
    $cert = New-Object System.Security.Cryptography.X509Certificates.X509Certificate2($PfxPath, $PfxPassword)
    Write-Host "    Sertifika: PFX ($PfxPath)" -ForegroundColor DarkGray
} elseif ($Thumbprint) {
    $cert = Get-Item "Cert:\CurrentUser\My\$Thumbprint" -ErrorAction SilentlyContinue
    if (-not $cert) {
        $cert = Get-Item "Cert:\LocalMachine\My\$Thumbprint" -ErrorAction SilentlyContinue
    }
    if (-not $cert) { throw "Thumbprint bulunamadi (CurrentUser/LocalMachine My): $Thumbprint" }
    Write-Host "    Sertifika: store thumbprint $Thumbprint" -ForegroundColor DarkGray
} else {
    $cert = Get-ChildItem Cert:\CurrentUser\My -CodeSigningCert -ErrorAction SilentlyContinue | Select-Object -First 1
    if (-not $cert) { throw "Kod imzalama sertifikasi bulunamadi. -PfxPath veya -Thumbprint verin." }
    Write-Host "    Sertifika: otomatik (CurrentUser\My ilk kod imzalama)" -ForegroundColor DarkGray
}

# İmzalanacak dosyalar: klasör ise içindeki exe/dll/msi, tek dosya ise kendisi.
if (Test-Path $PublishDir -PathType Leaf) {
    $binaries = @(Get-Item $PublishDir)
} else {
    if (-not (Test-Path $PublishDir)) {
        throw "Publish klasoru bulunamadi: $PublishDir. Once 'dotnet publish' calistirin."
    }
    $binaries = Get-ChildItem -Path $PublishDir -Recurse -Include *.exe, *.dll, *.msi |
        Where-Object { $_.FullName -notmatch '\\runtimes\\(linux|osx|unix)' }
}

if ($binaries.Count -eq 0) {
    throw "Imzalanacak exe/dll/msi bulunamadi."
}

$signed = 0; $failed = 0
foreach ($bin in $binaries) {
    Write-Host "  [+] $($bin.Name) ..." -NoNewline
    try {
        $sig = Set-AuthenticodeSignature -FilePath $bin.FullName -Certificate $cert `
            -TimestampServer $TimestampServer -HashAlgorithm SHA256
        if ($sig.Status -eq 'Valid') {
            Write-Host " OK ($($cert.Subject -replace ',.*',''))" -ForegroundColor Green
            $signed++
        } else {
            Write-Host " DOGRULAMA: $($sig.Status) ($($sig.StatusMessage))" -ForegroundColor Red
            $failed++
        }
    } catch {
        Write-Host " HATA: $($_.Exception.Message)" -ForegroundColor Red
        $failed++
    }
}

Write-Host ""
Write-Host "==> Imzalama tamam: $signed basarili, $failed basarisiz (toplam $($binaries.Count))" -ForegroundColor $(if ($failed -eq 0) {'Green'} else {'Yellow'})
if ($failed -gt 0) { exit 1 }
