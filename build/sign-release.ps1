#requires -Version 5.1
<#
.SYNOPSIS
    WinOptimizer — Release derlemesini Authenticode ile imzalar (master plan Bölüm 20.4).

.DESCRIPTION
    Publish çıktısındaki tüm exe/dll/msi dosyalarını EV (Extended Validation) veya
    standart kod imzalama sertifikasıyla imzalar. Çift zaman damgalı (RFC3161),
    SHA-256 digest. İmzalı dosyaları Verify-AuthenticodeSignature ile doğrular.

.PARAMETER PublishDir
    imzalanacak publish çıktı klasörü (varsayılan: src/WinOptimizer.App/bin/Release/.../publish).

.PARAMETER PfxPath
    Kod imzalama PFX dosyasının yolu. Yoksa yalnızca Windows certificate store kullanılır.

.PARAMETER PfxPassword
    PFX parolası (CI'da gizli değişken olmalı: $env:SIGN_PFX_PASSWORD).

.PARAMETER Thumbprint
    Certificate store'daki sertifikanın SHA-1 parmak izi (sha1).

.PARAMETER TimestampServer
    RFC3161 zaman damgası sunucusu (varsayılan: DigiCert).

.EXAMPLE
    .\sign-release.ps1 -Thumbprint ABCDEF0123456789 -PfxPath codesign.pfx
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

# signtool yolunu bul (Windows SDK).
function Resolve-SignTool {
    $candidates = @(
        "${env:ProgramFiles(x86)}\Windows Kits\10\bin\*\x64\signtool.exe",
        "${env:ProgramFiles(x86)}\Windows Kits\10\bin\*\x86\signtool.exe"
    ) | ForEach-Object { Get-ChildItem $_ -ErrorAction SilentlyContinue } | Sort-Object FullName -Descending
    $tool = $candidates | Select-Object -First 1
    if (-not $tool) {
        throw "signtool.exe bulunamadi. Windows SDK kurulu mu? (Bölüm 20.4)"
    }
    return $tool.FullName
}

# İmza argümanlarını oluştur — PFX dosyası veya certificate store.
function Build-SignArgs {
    param([string]$File)
    $args = @('sign', '/fd', 'sha256', '/tr', $TimestampServer, '/td', 'sha256')
    if ($PfxPath -and (Test-Path $PfxPath)) {
        if (-not $PfxPassword) { throw "PFX parolasi gerekli: -PfxPassword veya env SIGN_PFX_PASSWORD" }
        $args += @('/f', $PfxPath, '/p', $PfxPassword)
    } elseif ($Thumbprint) {
        $args += @('/sha1', $Thumbprint)
    } else {
        $args += '/a'  # Otomatik en uygun sertifika.
    }
    $args += $File
    return $args
}

Write-Host "==> WinOptimizer imzalama (master plan Bölüm 20.4)" -ForegroundColor Cyan
Write-Host "    PublishDir: $PublishDir"

if (-not (Test-Path $PublishDir)) {
    throw "Publish klasoru bulunamadi: $PublishDir. Once 'dotnet publish' calistirin."
}

$signtool = Resolve-SignTool
Write-Host "    signtool:   $signtool"

# İmzalanacak ikililer (DLL dahil — WIx MSI içindekilerin tamamı).
$binaries = Get-ChildItem -Path $PublishDir -Recurse -Include *.exe, *.dll, *.msi |
    Where-Object { $_.FullName -notmatch '\\(runtimes\\)\\(linux|osx|unix)' }

if ($binaries.Count -eq 0) {
    throw "Imzalanacak exe/dll/msi bulunamadi."
}

$signed = 0; $failed = 0
foreach ($bin in $binaries) {
    $signArgs = Build-SignArgs -File $bin.FullName
    Write-Host "  [+] $($bin.Name) ..." -NoNewline
    & $signtool @signArgs *> $null
    if ($LASTEXITCODE -eq 0) {
        # Doğrula — imza gerçekten geçerli mi?
        $sig = Get-AuthenticodeSignature -FilePath $bin.FullName
        if ($sig.Status -eq 'Valid') {
            Write-Host " OK ($($sig.SignerCertificate.Subject -replace ',.*',''))" -ForegroundColor Green
            $signed++
        } else {
            Write-Host " DOGRULAMA BASARISIZ ($($sig.Status))" -ForegroundColor Red
            $failed++
        }
    } else {
        Write-Host " SIGTOOL HATA (exit $LASTEXITCODE)" -ForegroundColor Red
        $failed++
    }
}

Write-Host ""
Write-Host "==> Imzalama tamam: $signed basarili, $failed basarisiz (toplam $($binaries.Count))" -ForegroundColor $(if ($failed -eq 0) {'Green'} else {'Yellow'})
if ($failed -gt 0) { exit 1 }
