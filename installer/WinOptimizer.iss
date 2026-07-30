; WinOptimizer — Inno Setup kurulum betiği. TEK dağıtım hattı.
;
; ELLE DERLEMEYIN. build/build-installer.ps1 üzerinden çalıştırın:
;   .\build\build-installer.ps1
; Sebebi: bu betik (a) self-contained publish çıktısına, (b) build/generate-license.ps1
; tarafından üretilen license.rtf'e ve (c) sürüm tanımlarına bağımlıdır. Elle `iscc`
; çağrısı bunlardan biri eksikken ya hata verir ya da hedefte açılmayan bir kurulum üretir.
;
; Sürüm build-installer.ps1 tarafından /D ile geçirilir; tek kaynak Directory.Build.props'tur.

#ifndef MyAppVersion
  #define MyAppVersion "0.0.0-dev"
#endif
#ifndef MyAppNumericVersion
  #define MyAppNumericVersion "0.0.0.0"
#endif

#define MyAppName "WinOptimizer"
#define MyAppPublisher "WinOptimizer Team"
#define MyAppUrl "https://github.com/barisugurluu/WinOptimizer"
#define MyAppExeName "WinOptimizer.App.exe"
#define MyAppServiceName "WinOptimizer.Service.exe"
#define MyAppCliName "WinOptimizer.Cli.exe"
#define MyAppIcon "..\src\WinOptimizer.App\Resources\WinOptimizer.ico"

[Setup]
AppId={{A1B2C3D4-E5F6-7890-ABCD-EF1234567890}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppUrl}
AppSupportURL={#MyAppUrl}/issues
AppUpdatesURL={#MyAppUrl}/releases
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
OutputDir=build
OutputBaseFilename={#MyAppName}-{#MyAppVersion}-setup
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
; EULA ekrani. license.rtf, build\generate-license.ps1 tarafindan docs\EULA.md'den
; uretilir (commit edilmez) — kurulumda gosterilen metin Markdown'dan ayrisamaz.
LicenseFile=license.rtf
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
; Yonetici SART: uygulama app.manifest ile requireAdministrator, servis LocalSystem olarak
; kurulur ve {autopf} altina yazilir. PrivilegesRequiredOverridesAllowed BILINCLI OLARAK
; ayarlanmadi: kullaniciya "yalnizca benim icin kur" seceneginin verilmesi, sonrasinda
; servisin kurulamadigi ve uygulamanin yine UAC istedigi bozuk bir kurulum modu yaratir.
PrivilegesRequired=admin
; Windows 10 2004 (19041) alt siniri. AYNI SAYI uc yerde: burada,
; installer/winget/*.installer.yaml MinimumOSVersion ve
; WinOptimizer.Core WindowsVersionInfo.Windows10Build2004.
MinVersion=10.0.19041
; Dosya/kurulum meta verisi — imzasiz dagitimda kullanicinin gorebilecegi tek kimlik bilgisi.
VersionInfoVersion={#MyAppNumericVersion}
VersionInfoProductVersion={#MyAppNumericVersion}
VersionInfoProductName={#MyAppName}
VersionInfoCompany={#MyAppPublisher}
VersionInfoDescription={#MyAppName} Kurulum
SetupIconFile={#MyAppIcon}
UninstallDisplayIcon={app}\{#MyAppExeName}
UninstallDisplayName={#MyAppName} {#MyAppVersion}
; PATH degistirildigi icin ortam yayini (WM_SETTINGCHANGE) gerekir — bkz. [Registry].
ChangesEnvironment=yes
; Calisan ornek uzerine yukseltme: App bu mutex'i olusturur (App.xaml.cs), Inno onu gorup
; uygulamayi kapatmayi teklif eder. Ad birebir ayni olmak zorunda.
AppMutex=WinOptimizerAppSingleInstance
CloseApplications=yes
RestartApplications=no
;
; KOD IMZALAMA YOKTUR — bilincli karar (bkz. docs/KURULUM.md "SmartScreen uyarisi").
; Buraya bir SignTool satiri EKLEMEYIN: kendinden imzali bir sertifikayla imzalamak,
; hedef PC'de "gecersiz imza" anlamina gelir ve imzasiz olmaktan DAHA KOTUDUR.
; Gercek (OV/EV) sertifika alinirsa build/sign-release.ps1 altyapisi hazirdir.

[Languages]
Name: "turkish"; MessagesFile: "compiler:Languages\Turkish.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
; VARSAYILAN KAPALI (unchecked) — iki sebep:
;  1) Guvenli varsayilanlar ilkesi (CLAUDE.md §3.4): arka planda otomatik mudahale eden
;     bir servis kullanicinin acik onayi olmadan kurulmaz.
;  2) Servis kurulumu kurulumun en son adimidir; opsiyonel kalmasi kurulumun tamamlanmasini
;     asla servise bagimli kilmaz.
; Kurulumdan sonra uygulama icindeki Guard sekmesinden de kurulabilir.
Name: "installservice"; Description: "RealtimeGuard hizmetini kur ve başlat (isteğe bağlı)"; GroupDescription: "Ek bileşenler:"; Flags: unchecked

[Files]
; Uygulama — SELF-CONTAINED publish cikti klasoru (.NET runtime gomulu).
; Hedef PC'de .NET kurulu olmasi GEREKMEZ. build-installer.ps1 publish sonrasi
; hostfxr.dll/coreclr.dll kontrolu yapar; framework-dependent bir agac buraya girmez.
Source: "..\src\WinOptimizer.App\bin\Release\net8.0-windows\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"; Comment: "Windows bakım ve optimizasyon"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"; Comment: "Windows bakım ve optimizasyon"; Tasks: desktopicon

[Registry]
; Kurulum dizinini sistem PATH'ine ekler. Gerekcesi:
;  - `WinOptimizer.Cli status` destek konusmalarinda dogrudan yazilabilir olsun,
;  - SchedulerService'in olusturdugu haftalik gorevdeki CLI yolu cozulebilir kalsin,
;  - winget manifestindeki Commands: [WinOptimizer.App, WinOptimizer.Cli] iddiasi dogru olsun.
Root: HKLM; Subkey: "SYSTEM\CurrentControlSet\Control\Session Manager\Environment"; \
    ValueType: expandsz; ValueName: "Path"; ValueData: "{olddata};{app}"; \
    Check: NeedsAddPath(ExpandConstant('{app}'))

[Run]
; Servis kurulumu tek bir verb'e indirildi. ESKI HATA: burada `Service.exe install`
; cagriliyordu; exe bu argumani tanimadigi icin normal worker olarak baslayip hic
; cikmiyordu ve Inno onu bekledigi icin KURULUM SONSUZA KADAR DONUYORDU. Ustelik
; `sc create` hic yoktu, ardindan gelen `sc config/description/start` satirlari 1060
; hatasi aliyordu. Artik ServiceInstaller create/config + description + failure + start
; adimlarini yapip tek bir exit code ile cikiyor; `waituntilterminated` bu yuzden guvenli.
Filename: "{app}\{#MyAppServiceName}"; Parameters: "install-service"; \
    StatusMsg: "RealtimeGuard hizmeti kuruluyor..."; \
    Flags: runhidden waituntilterminated; Tasks: installservice
; Kurulum sonu "uygulamayi baslat" kutusu. runasoriginaluser YOK: setup zaten yukseltilmis
; ve App requireAdministrator; ayni hesapta ikinci bir UAC istemini onler.
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#MyAppName}}"; \
    WorkingDir: "{app}"; Flags: nowait postinstall skipifsilent

[UninstallRun]
; [UninstallRun] girdileri dosyalar silinmeden ONCE calisir — exe hala yerinde.
Filename: "{app}\{#MyAppServiceName}"; Parameters: "uninstall-service"; \
    Flags: runhidden waituntilterminated; RunOnceId: "RemoveGuardService"

[UninstallDelete]
Type: filesandordirs; Name: "{app}"

[Code]
{ PATH'e ekleme gerekli mi? Zaten varsa tekrar eklenmez (her yukseltmede PATH'i
  sismekten korur). Karsilastirma noktali virgullerle sarilarak yapilir ki
  "C:\Program Files\WinOptimizerX" gibi bir onek yanlis pozitif vermesin. }
function NeedsAddPath(Param: string): Boolean;
var
  OrigPath: string;
begin
  if not RegQueryStringValue(HKEY_LOCAL_MACHINE,
    'SYSTEM\CurrentControlSet\Control\Session Manager\Environment',
    'Path', OrigPath)
  then begin
    Result := True;
    exit;
  end;
  Result := Pos(';' + Uppercase(Param) + ';', ';' + Uppercase(OrigPath) + ';') = 0;
end;

{ Kaldirma sonrasi kullanici verisi. VARSAYILAN HAYIR ve asla sessizce silinmez:
  %ProgramData%\WinOptimizer icindeki change journal, geri alma islemlerinin TEK
  veri kaynagidir; onu silmek yapilmis tum degisiklikleri kalici hale getirir. }
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  DataDir: string;
begin
  if CurUninstallStep = usPostUninstall then
  begin
    DataDir := ExpandConstant('{commonappdata}\WinOptimizer');
    if DirExists(DataDir) then
    begin
      if MsgBox('Ayarlar, günlükler ve geri alma geçmişi de silinsin mi?' + #13#10#13#10 +
                DataDir + #13#10#13#10 +
                'Geri alma geçmişi silinirse WinOptimizer''ın yaptığı değişiklikler ' +
                'artık geri alınamaz. Emin değilseniz "Hayır" seçin.',
                mbConfirmation, MB_YESNO or MB_DEFBUTTON2) = IDYES then
      begin
        DelTree(DataDir, True, True, True);
      end;
    end;
  end;
end;
