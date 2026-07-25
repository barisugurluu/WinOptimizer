#requires -Version 5.1
<#
.SYNOPSIS
    WinOptimizer — tam paketleme hattı: publish + imzala + MSI derle (master plan Bölüm 20).

.DESCRIPTION
    1) dotnet publish (Release, tek dosya/çerçeve bağımlı)
    2) Authenticode imzala (sign-release.ps1)
    3) WiX MSI üret (per-machine + Service LocalSystem + CLI tek kurulum)
    4) MSI'yı imzala

.PARAMETER SkipSign
    İmzalama adımını atla (geliştirme derlemeleri için).

.PARAMETER Configuration
    Derleme yapılandırması (varsayılan: Release).

.PARAMETER PfxPath
    Kod imzalama PFX dosyasının yolu (sign-release.ps1'e iletilir). Yoksa certificate store kullanılır.

.PARAMETER PfxPassword
    PFX parolası (CI'da gizli değişken: $env:SIGN_PFX_PASSWORD).

.PARAMETER Thumbprint
    Certificate store'daki sertifikanın SHA-1 parmak izi.

.EXAMPLE
    .\build-installer.ps1                 # tam hat
    .\build-installer.ps1 -SkipSign       # geliştirme: imzasız MSI
    .\build-installer.ps1 -PfxPath codesign.pfx -PfxPassword $env:SIGN_PFX_PASSWORD
#>
[CmdletBinding()]
param(
    [switch]$SkipSign,
    [ValidateSet('Debug','Release')][string]$Configuration = 'Release',
    [string]$PfxPath,
    [string]$PfxPassword = $env:SIGN_PFX_PASSWORD,
    [string]$Thumbprint
)

$ErrorActionPreference = 'Stop'
$root = Resolve-Path (Join-Path $PSScriptRoot '..')
Set-Location $root

$solution = Join-Path $root 'WinOptimizer.sln'
$publishBase = Join-Path $root 'src\WinOptimizer.App\bin\Release\net8.0-windows\publish'
$signScript = Join-Path $PSScriptRoot 'sign-release.ps1'

# İmzalama kimlik bilgileri — yalnızca verilenler sign-release.ps1'e iletilir.
$signCredentials = @{}
if ($PfxPath)     { $signCredentials['PfxPath'] = $PfxPath }
if ($PfxPassword) { $signCredentials['PfxPassword'] = $PfxPassword }
if ($Thumbprint)  { $signCredentials['Thumbprint'] = $Thumbprint }

Write-Host "==> WinOptimizer paketleme hatti (Bölüm 20)" -ForegroundColor Cyan

# --- 1) Derle + Test ---
Write-Host "[1/4] Derleme + test..." -ForegroundColor Yellow
dotnet build $solution -c $Configuration
if ($LASTEXITCODE -ne 0) { throw "Derleme basarisiz (Bölüm 8.6: sifir uyari)." }
dotnet test $solution -c $Configuration --no-build --verbosity minimal
if ($LASTEXITCODE -ne 0) { throw "Testler basarisiz." }

# --- 2) Publish (App + Service + CLI) ---
Write-Host "[2/4] Publish..." -ForegroundColor Yellow
dotnet publish $solution -c $Configuration -o $publishBase -p:PublishSingleFile=false
if ($LASTEXITCODE -ne 0) { throw "Publish basarisiz." }

# --- 3) İmzala (opsiyonel) ---
if (-not $SkipSign) {
    Write-Host "[3/4] Authenticode imzalama..." -ForegroundColor Yellow
    & $signScript -PublishDir $publishBase @signCredentials
    if ($LASTEXITCODE -ne 0) { throw "Imzalama basarisiz." }
} else {
    Write-Host "[3/4] Imzalama ATLANDI (-SkipSign)" -ForegroundColor DarkYellow
}

# --- 4) Payload.wxs üret (heat alternatifi) + WiX MSI ---
Write-Host "[4/5] Payload.wxs uretiliyor (generate-payload.ps1)..." -ForegroundColor Yellow
& (Join-Path $PSScriptRoot 'generate-payload.ps1') -PublishDir $publishBase
if ($LASTEXITCODE -ne 0) { throw "Payload.wxs uretimi basarisiz." }

Write-Host "[5/5] WiX MSI paketi..." -ForegroundColor Yellow
$wixProj = Join-Path $root 'installer\wix\WinOptimizer.wixproj'
if (-not (Test-Path $wixProj)) { throw "WiX proje yok: $wixProj" }
dotnet build $wixProj -c $Configuration
if ($LASTEXITCODE -ne 0) { throw "WiX MSI derleme basarisiz." }

$msi = Get-ChildItem (Join-Path $root 'installer\wix\bin') -Filter '*.msi' -Recurse | Select-Object -First 1
if ($msi) {
    Write-Host ""
    Write-Host "==> MSI uretildi: $($msi.FullName)" -ForegroundColor Green
    if (-not $SkipSign) {
        & $signScript -PublishDir $msi.DirectoryName @signCredentials
        if ($LASTEXITCODE -ne 0) { throw "MSI imzalama basarisiz." }
    }
} else {
    throw "MSI bulunamadi (installer/wix/bin altinda degil)."
}
