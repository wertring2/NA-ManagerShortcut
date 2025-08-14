; Inno Setup Script for Network Adapter Manager
; Requires Inno Setup 6.x from https://jrsoftware.org/isinfo.php

#define MyAppName "Network Adapter Manager"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "Q WAVE COMPANY LIMITED"
#define MyAppURL "https://qwave.co.th"
#define MyAppExeName "NA-ManagerShortcut.exe"

[Setup]
AppId={{A7B3C4D5-E6F7-8901-2345-6789ABCDEF01}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf64}\{#MyAppName}
DisableProgramGroupPage=yes
LicenseFile=LICENSE.txt
OutputDir=.
OutputBaseFilename=NetworkAdapterManager-Setup-{#MyAppVersion}
SetupIconFile=NA-ManagerShortcut\favicon.ico
Compression=lzma
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
ArchitecturesInstallIn64BitMode=x64

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "startup"; Description: "Start automatically with Windows"; GroupDescription: "Additional options:"; Flags: unchecked

[Files]
Source: "NA-ManagerShortcut\bin\Release\net9.0-windows\NA-ManagerShortcut.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "NA-ManagerShortcut\bin\Release\net9.0-windows\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "NetworkAdapterManager"; ValueData: """{app}\{#MyAppExeName}"""; Flags: uninsdeletevalue; Tasks: startup

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[UninstallRun]
Filename: "taskkill"; Parameters: "/F /IM {#MyAppExeName}"; Flags: runhidden