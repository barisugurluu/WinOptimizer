#requires -Version 5.1
<#
.SYNOPSIS
    WinOptimizer — TEK paketleme hatti: derle + test + self-contained publish + setup.exe.

.DESCRIPTION
    Bu betik projenin tek dagitim hattidir. Cikti:
        installer\build\WinOptimizer-<surum>-setup.exe   (+ .sha256 yan dosyasi)

    Adimlar:
      1) Surumu Directory.Build.props'tan oku (tek kaynak)
      2) dotnet build + dotnet test
      3) SELF-CONTAINED publish (App + Service + CLI) — .NET runtime gomulu
      4) Publish saglik kontrolu (hostfxr/coreclr/uc exe) — hedefte acilmayan paket uretmeyi onler
      5) license.rtf uret (docs\EULA.md'den)
      6) ISCC (Inno Setup) ile setup.exe derle
      7) SHA256 hesapla ve yan dosyaya yaz

    KOD IMZALAMA YOKTUR — bilincli karar. Dagitim imzasizdir; SmartScreen talimatlari ve
    SHA256 dogrulamasi docs\KURULUM.md'de anlatilir. build\sign-release.ps1 diskte durur
    ama HICBIR YERDEN CAGRILMAZ (gercek OV/EV sertifikasi alinirsa hazir altyapi).
    Kendinden imzali sertifika ASLA kullanilmaz: hedef PC'de "gecersiz imza" anlamina gelir
    ve imzasiz olmaktan daha kotudur.

    MSI/WiX hatti KALDIRILDI (bkz. docs\KURULUM.md "Neden MSI degil?").

.PARAMETER SkipTests
    dotnet test adimini atla (CI'da testler ayri job adiminda kosuyorsa).

.PARAMETER FrameworkDependent
    Runtime'i GOMME (yalnizca gelistirme/tanilama icin). Uretilen kurulum, hedef PC'de
    .NET 8 Desktop Runtime kurulu olmasini gerektirir — dagitim icin KULLANMAYIN.

.PARAMETER IsccPath
    ISCC.exe yolu. Verilmezse kayit defteri ve bilinen kurulum konumlari taranir.

.EXAMPLE
    .\build-installer.ps1
.EXAMPLE
    .\build-installer.ps1 -SkipTests
#>
[CmdletBinding()]
param(
    [switch]$SkipTests,
    [switch]$FrameworkDependent,
    [ValidateSet('Debug', 'Release')][string]$Configuration = 'Release',
    [string]$IsccPath
)

$ErrorActionPreference = 'Stop'
$root = Resolve-Path (Join-Path $PSScriptRoot '..')
Set-Location $root

$solution = Join-Path $root 'WinOptimizer.sln'
$publishBase = Join-Path $root 'src\WinOptimizer.App\bin\Release\net8.0-windows\publish'
$issFile = Join-Path $root 'installer\WinOptimizer.iss'

function Resolve-Iscc {
    <#
      ISCC.exe konumu kuruluma gore degisir ve PATH'e EKLENMEZ:
        - winget/user-scope kurulumu: %LOCALAPPDATA%\Programs\Inno Setup 6
        - klasik/choco kurulumu:      %ProgramFiles(x86)%\Inno Setup 6
      Bu yuzden once kayit defteri (kesin bilgi), sonra bilinen yollar denenir.
    #>
    param([string]$Explicit)

    if ($Explicit) {
        if (-not (Test-Path $Explicit)) { throw "ISCC bulunamadi: $Explicit" }
        return (Resolve-Path $Explicit).Path
    }

    $uninstallKeys = @(
        'HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Inno Setup 6_is1',
        'HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\Inno Setup 6_is1',
        'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Inno Setup 6_is1'
    )
    foreach ($key in $uninstallKeys) {
        try {
            $location = (Get-ItemProperty -Path $key -Name 'InstallLocation' -ErrorAction Stop).InstallLocation
            if ($location) {
                $candidate = Join-Path $location 'ISCC.exe'
                if (Test-Path $candidate) { return (Resolve-Path $candidate).Path }
            }
        }
        catch { <# kayit yok, sonraki adaya gec #> }
    }

    $paths = @(
        (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe'),
        (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe'),
        (Join-Path $env:ProgramFiles 'Inno Setup 6\ISCC.exe')
    )
    foreach ($candidate in $paths) {
        if ($candidate -and (Test-Path $candidate)) { return (Resolve-Path $candidate).Path }
    }

    $onPath = Get-Command 'iscc.exe' -ErrorAction SilentlyContinue
    if ($onPath) { return $onPath.Source }

    throw @'
Inno Setup 6 bulunamadi. Kurulum:
    winget install JRSoftware.InnoSetup
veya CI'da:
    choco install innosetup --no-progress -y
Alternatif olarak -IsccPath ile ISCC.exe yolunu verin.
'@
}

Write-Host '==> WinOptimizer paketleme hatti' -ForegroundColor Cyan

# --- 1) Surum (tek kaynak: Directory.Build.props) ---
[xml]$props = Get-Content (Join-Path $root 'Directory.Build.props')
$prefix = ($props.Project.PropertyGroup.VersionPrefix | Where-Object { $_ }) -as [string]
$suffix = ($props.Project.PropertyGroup.VersionSuffix | Where-Object { $_ }) -as [string]
if (-not $prefix) { throw 'Directory.Build.props icinde VersionPrefix yok.' }
$displayVersion = if ($suffix) { "$prefix-$suffix" } else { $prefix }
$numericVersion = "$prefix.0"
Write-Host "    Surum: $displayVersion (VersionInfo: $numericVersion)" -ForegroundColor Gray

# --- 2) Derle + test ---
Write-Host '[1/6] Derleme...' -ForegroundColor Yellow
dotnet build $solution -c $Configuration
if ($LASTEXITCODE -ne 0) { throw 'Derleme basarisiz (sifir uyari hedefi).' }

if ($SkipTests) {
    Write-Host '[2/6] Testler ATLANDI (-SkipTests)' -ForegroundColor DarkYellow
}
else {
    Write-Host '[2/6] Testler...' -ForegroundColor Yellow
    dotnet test $solution -c $Configuration --no-build --verbosity minimal
    if ($LASTEXITCODE -ne 0) { throw 'Testler basarisiz — paketleme durduruldu.' }
}

# --- 3) Publish (App + Service + CLI) ---
# DIKKAT: cozumun tamami publish EDILMEZ. `dotnet publish <sln>` test projelerini de ayni
# klasore koyar; bu da xunit/Moq/FluentAssertions/testhost ikililerinin son kullaniciya
# gonderilmesine yol acar. Yalnizca dagitilacak uc proje yayinlanir.
Write-Host '[3/6] Publish (App + Service + CLI)...' -ForegroundColor Yellow
# SIRA ONEMLI. `dotnet publish -o <dir>` ayni klasore yapilan onceki publish'lerden kalan
# "sahipsiz" dosyalari TEMIZLER. App en son publish edilirse, yalnizca Cli'nin referansladigi
# WinOptimizer.Modules.BenchmarkEngine.dll siliniyor ve `WinOptimizer.Cli benchmark` komutu
# kurulumda calisan bir pakette dosya-bulunamadi hatasiyla patliyordu.
# Bu yuzden EN GENIS kume (App) once, ek dosyasi olanlar (Cli) en son yayinlanir.
# Adim 4'teki saglik kontrolu bu dosyayi ayrica dogrular.
$shippingProjects = @(
    'src\WinOptimizer.App\WinOptimizer.App.csproj',
    'src\WinOptimizer.Service\WinOptimizer.Service.csproj',
    'src\WinOptimizer.Cli\WinOptimizer.Cli.csproj'
)

if ($FrameworkDependent) {
    Write-Host '    Mod: FRAMEWORK-DEPENDENT — hedefte .NET 8 Desktop Runtime GEREKLI.' -ForegroundColor Red
    Write-Host '         Dagitim icin kullanmayin (kullanici "You must install .NET" hatasi alir).' -ForegroundColor Red
    $publishArgs = @('-p:PublishSingleFile=false')
}
else {
    Write-Host '    Mod: SELF-CONTAINED (runtime gomulu, hedefte .NET gerekmez)' -ForegroundColor Green
    # SatelliteResourceLanguages: WPF cercevesinin ~13 dil uydu klasorunu (cs/de/es/fr/it/
    # ja/ko/pl/pt-BR/ru/zh-Hans/zh-Hant...) atar. Uygulama TR/EN yerelleştirildigi icin
    # digerleri olu agirlik; kurulum boyutunu belirgin dusurur.
    # %3B = kacisli noktali virgul. Duz ';' MSBuild'e ULASMAZ: dotnet CLI argumani
    # boler ve "MSB1006: Ozellik gecerli degil. Anahtar: tr" hatasi alinir.
    $publishArgs = @(
        '-r', 'win-x64',
        '--self-contained', 'true',
        '-p:PublishSingleFile=false',
        '-p:SatelliteResourceLanguages=en%3Btr'
    )
}

# Publish klasorunu once temizle: self-contained/framework-dependent modlari arasi runtime
# DLL karismasini onler. `dotnet publish -o <dir>` mevcut icerigi silmez.
if (Test-Path $publishBase) { Remove-Item $publishBase -Recurse -Force }

foreach ($proj in $shippingProjects) {
    dotnet publish (Join-Path $root $proj) -c $Configuration -o $publishBase @publishArgs
    if ($LASTEXITCODE -ne 0) { throw "Publish basarisiz: $proj" }
}

# --- 4) Publish saglik kontrolu ---
# Bu kontrol olmadigi icin daha once 3.2 MB'lik framework-dependent bir paket "hazir"
# sayilmis ve hicbir hedef PC'de acilmamisti. Tek satirlik guvence:
Write-Host '[4/6] Publish saglik kontrolu...' -ForegroundColor Yellow
$requiredFiles = @(
    'WinOptimizer.App.exe', 'WinOptimizer.Service.exe', 'WinOptimizer.Cli.exe',
    # Yalnizca Cli'nin referansladigi modul: publish sirasi bozulursa ilk kaybolan bu olur.
    'WinOptimizer.Modules.BenchmarkEngine.dll',
    # App'in EN kaynak uydusu (SatelliteResourceLanguages filtresi fazla agresiflesirse duser).
    'en\WinOptimizer.App.resources.dll'
)
if (-not $FrameworkDependent) {
    $requiredFiles += @('hostfxr.dll', 'coreclr.dll', 'System.Private.CoreLib.dll')
}
foreach ($file in $requiredFiles) {
    if (-not (Test-Path (Join-Path $publishBase $file))) {
        throw "Publish eksik: $file — kurulum hedefte acilmaz, paketleme durduruldu."
    }
}
$published = Get-ChildItem $publishBase -Recurse -File
Write-Host ("    {0} dosya, {1:N1} MB" -f $published.Count,
    (($published | Measure-Object Length -Sum).Sum / 1MB)) -ForegroundColor Gray

# --- 5) license.rtf ---
# ZORUNLU SIRA: .iss icindeki LicenseFile=license.rtf olmadan iscc hata verir, ve bu dosya
# gitignore'da (docs\EULA.md'den uretilir). Betik icinde zorlanir; "elle calistirmayi
# hatirla" varsayimi tam olarak boyle kirilir.
Write-Host '[5/6] license.rtf uretiliyor...' -ForegroundColor Yellow
& (Join-Path $PSScriptRoot 'generate-license.ps1')
if ($LASTEXITCODE -ne 0) { throw 'license.rtf uretimi basarisiz.' }

# --- 6) setup.exe ---
Write-Host '[6/6] Inno Setup (ISCC)...' -ForegroundColor Yellow
$iscc = Resolve-Iscc -Explicit $IsccPath
Write-Host "    ISCC: $iscc" -ForegroundColor Gray

& $iscc "/DMyAppVersion=$displayVersion" "/DMyAppNumericVersion=$numericVersion" $issFile
if ($LASTEXITCODE -ne 0) { throw "ISCC basarisiz (exit $LASTEXITCODE)." }

$setup = Join-Path $root ("installer\build\WinOptimizer-$displayVersion-setup.exe")
if (-not (Test-Path $setup)) { throw "Kurulum dosyasi uretilmedi: $setup" }

# --- 7) SHA256 yan dosyasi ---
# Imzasiz dagitimda kullanicinin indirdigi dosyayi dogrulamasinin TEK yolu bu hash.
# Bicim `sha256sum` uyumlu: "<hash>  <dosyaadi>"
$hash = (Get-FileHash $setup -Algorithm SHA256).Hash
"$hash  $(Split-Path $setup -Leaf)" | Set-Content "$setup.sha256" -Encoding ascii

$setupInfo = Get-Item $setup
Write-Host ''
Write-Host '==> KURULUM HAZIR' -ForegroundColor Green
Write-Host ("    Dosya : {0}" -f $setupInfo.FullName)
Write-Host ("    Boyut : {0:N1} MB" -f ($setupInfo.Length / 1MB))
Write-Host ("    SHA256: {0}" -f $hash)
Write-Host ("    Surum : {0}" -f $displayVersion)
if ($FrameworkDependent) {
    Write-Host '    UYARI : framework-dependent — hedefte .NET 8 Desktop Runtime gerekir.' -ForegroundColor Red
}
