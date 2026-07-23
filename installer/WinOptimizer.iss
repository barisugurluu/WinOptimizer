; WinOptimizer — Inno Setup kurulum betiği (master plan Bölüm v4.0 & Faz 9)
; Derleme: iscc WinOptimizer.iss
; App + Service + CLI'yi birlikte paketler. EV kod imzalamadan önce çıkış yapar.

#define MyAppName "WinOptimizer"
#define MyAppVersion "0.1.0"
#define MyAppPublisher "WinOptimizer Team"
#define MyAppExeName "WinOptimizer.App.exe"
#define MyAppServiceName "WinOptimizer.Service.exe"
#define MyAppCliName "WinOptimizer.Cli.exe"

[Setup]
AppId={{A1B2C3D4-E5F6-7890-ABCD-EF1234567890}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
OutputDir=build
OutputBaseFilename=WinOptimizer-{#MyAppVersion}-setup
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=admin
; Kod imzalama (sertifika varsa aç):
; SignTool=signtool sign /a /f codesign.pfx /p $p /tr http://timestamp.digicert.com /td sha256 /fd sha256 $f

[Languages]
Name: "turkish"; MessagesFile: "compiler:Languages\Turkish.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "installservice"; Description: "RealtimeGuard hizmetini kur ve başlat"; GroupDescription: "Ek bileşenler:"

[Files]
; Uygulama (Release derlemesi)
Source: "..\src\WinOptimizer.App\bin\Release\net8.0-windows\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
; Kurulum sonrası servisi kaydet (kullanıcı isterse)
Filename: "{app}\{#MyAppServiceName}"; Parameters: "install"; Flags: runhidden; Tasks: installservice
Filename: "sc.exe"; Parameters: "config WinOptimizerGuard start= auto"; Flags: runhidden; Tasks: installservice
Filename: "sc.exe"; Parameters: "description WinOptimizerGuard ""WinOptimizer gerçek zamanlı koruma servisi"""; Flags: runhidden; Tasks: installservice
Filename: "sc.exe"; Parameters: "start WinOptimizerGuard"; Flags: runhidden; Tasks: installservice

[UninstallRun]
; Kaldırma sırasında servisi durdur ve sil
Filename: "sc.exe"; Parameters: "stop WinOptimizerGuard"; Flags: runhidden; RunOnceId: "StopService"
Filename: "sc.exe"; Parameters: "delete WinOptimizerGuard"; Flags: runhidden; RunOnceId: "DeleteService"

[UninstallDelete]
Type: filesandordirs; Name: "{app}"
