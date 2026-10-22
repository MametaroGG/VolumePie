; VolumePie Inno Setup Installer Script
#define MyAppName "VolumePie"
#define MyAppVersion "1.0.2"
#define MyAppPublisher "VolumePie Project"
#define MyAppExeName "VolumePie.exe"
#define PublishDir "..\publish"

[Setup]
AppId={{8F9E4A32-6D18-4C7A-9B3E-25D74A1C8E90}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
OutputDir=..\dist
OutputBaseFilename=VolumePie-Setup-{#MyAppVersion}
SetupIconFile=..\src\VolumePie\Assets\app.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
Compression=lzma2/fast
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
AppMutex=VolumePie_SingleInstance_Mutex_v1
CloseApplications=yes

[Languages]
Name: "japanese"; MessagesFile: "compiler:Languages\Japanese.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "chinesesimplified"; MessagesFile: "Languages\ChineseSimplified.isl"
Name: "chinesetraditional"; MessagesFile: "Languages\ChineseTraditional.isl"
Name: "korean"; MessagesFile: "compiler:Languages\Korean.isl"

[CustomMessages]
japanese.StartupTask=Windows 起動時に VolumePie を自動起動する
japanese.StartupGroup=スタートアップ設定:
japanese.UninstallProgram=VolumePie をアンインストール

english.StartupTask=Launch VolumePie automatically on Windows startup
english.StartupGroup=Startup settings:
english.UninstallProgram=Uninstall VolumePie

chinesesimplified.StartupTask=Windows 启动时自动运行 VolumePie
chinesesimplified.StartupGroup=开机启动设置:
chinesesimplified.UninstallProgram=卸载 VolumePie

chinesetraditional.StartupTask=Windows 啟動時自動執行 VolumePie
chinesetraditional.StartupGroup=開機啟動設定:
chinesetraditional.UninstallProgram=解除安裝 VolumePie

korean.StartupTask=Windows 시작 시 VolumePie 자동 실행
korean.StartupGroup=시작 프로그램 설정:
korean.UninstallProgram=VolumePie 제거

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"
Name: "startup"; Description: "{cm:StartupTask}"; GroupDescription: "{cm:StartupGroup}"

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\{cm:UninstallProgram}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "VolumePie"; ValueData: """{app}\{#MyAppExeName}"" --minimized"; Flags: uninsdeletevalue; Tasks: startup

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent
