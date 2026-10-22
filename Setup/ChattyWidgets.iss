; Script generated for ChattyWidgets Inno Setup Installer
#define MyAppName "ChattyWidgets"
#define MyAppVersion "1.0.1"
#define MyAppPublisher "ChattyApps"
#define MyAppURL "https://chattyapps.dev"
#define MyAppExeName "ChattyWidgets.exe"

[Setup]
AppId={{D37E6F22-540B-4819-B452-9A82D6027EAA}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={localappdata}\Programs\{#MyAppName}
DisableProgramGroupPage=yes
OutputBaseFilename=ChattyWidgets_Setup_v{#MyAppVersion}
SetupIconFile=..\app.ico
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=lowest
UninstallDisplayIcon={app}\{#MyAppExeName}

[Languages]
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"
Name: "autostart"; Description: "Запускать ChattyWidgets автоматически при старте Windows"; GroupDescription: "Параметры запуска:"
Name: "agentskill"; Description: "🤖 Установить скилл для AI-ассистентов (/chw в ~/.agents/skills/chw)"; GroupDescription: "Интеграции для разработчиков:"
Name: "openwebsite"; Description: "🌐 Открыть официальный сайт chattyapps.dev"; GroupDescription: "После установки:"

[Files]
Source: "..\bin\Release\net8.0-windows10.0.19041.0\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\.agents\skills\chw\*"; DestDir: "{userprofile}\.agents\skills\chw"; Flags: ignoreversion recursesubdirs createallsubdirs; Tasks: agentskill

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Registry]
; Run on Windows startup
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "ChattyWidgets"; ValueData: """{app}\{#MyAppExeName}"" --minimized"; Flags: uninsdeletevalue; Tasks: autostart

; File Association .chw
Root: HKCU; Subkey: "Software\Classes\.chw"; ValueType: string; ValueData: "ChattyWidgets.Package"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\ChattyWidgets.Package"; ValueType: string; ValueData: "ChattyWidgets Widget Package"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\ChattyWidgets.Package\DefaultIcon"; ValueType: string; ValueData: "{app}\{#MyAppExeName},0"
Root: HKCU; Subkey: "Software\Classes\ChattyWidgets.Package\shell\open\command"; ValueType: string; ValueData: """{app}\{#MyAppExeName}"" ""%1"""

; File Association .chwp
Root: HKCU; Subkey: "Software\Classes\.chwp"; ValueType: string; ValueData: "ChattyWidgets.Preset"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\ChattyWidgets.Preset"; ValueType: string; ValueData: "ChattyWidgets Preset Package"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\ChattyWidgets.Preset\DefaultIcon"; ValueType: string; ValueData: "{app}\{#MyAppExeName},0"
Root: HKCU; Subkey: "Software\Classes\ChattyWidgets.Preset\shell\open\command"; ValueType: string; ValueData: """{app}\{#MyAppExeName}"" ""%1"""

[Run]
Filename: "{#MyAppURL}"; Flags: shellexec runminimized postinstall nowait; Description: "Открыть сайт {#MyAppURL}"; Tasks: openwebsite
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent
