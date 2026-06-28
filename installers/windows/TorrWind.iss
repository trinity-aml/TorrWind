#define AppName "TorrWind"
#ifndef AppVersion
#define AppVersion "0.1.0"
#endif
#define Publisher "TorrWind contributors"
#define PublishDir "..\..\artifacts\publish\TorrWind"

[Setup]
AppId={{C82F63B6-1D78-4D3D-8A4E-8AE73E52685E}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#Publisher}
DefaultDirName={autopf}\TorrWind
DefaultGroupName=TorrWind
OutputDir=..\..\artifacts\installer
OutputBaseFilename=TorrWind-{#AppVersion}-win-x64
Compression=lzma2
SolidCompression=yes
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64
PrivilegesRequired=admin
LicenseFile=..\..\LICENSE
UninstallDisplayIcon={app}\TorrWind.exe
SetupLogging=yes
WizardStyle=modern

[Dirs]
Name: "{commonappdata}\TorrWind"; Permissions: users-modify
Name: "{commonappdata}\TorrWind\TorrServer"; Permissions: users-modify

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\TorrWind"; Filename: "{app}\TorrWind.exe"
Name: "{autodesktop}\TorrWind"; Filename: "{app}\TorrWind.exe"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Create a desktop icon"; GroupDescription: "Additional icons:"
Name: "startup"; Description: "Start TorrWind with Windows"; GroupDescription: "Startup:"
Name: "installservice"; Description: "Install TorrWindService for local TorrServer"; GroupDescription: "Windows service:"
Name: "installservice\startservice"; Description: "Start TorrWindService after installation"; Flags: unchecked

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "TorrWind"; ValueData: """{app}\TorrWind.exe"" --minimized"; Flags: uninsdeletevalue; Tasks: startup

[Run]
Filename: "{app}\TorrWind.Service.exe"; Parameters: "install"; StatusMsg: "Installing TorrWindService..."; Flags: runhidden waituntilterminated; Tasks: installservice
Filename: "{app}\TorrWind.Service.exe"; Parameters: "start"; StatusMsg: "Starting TorrWindService..."; Flags: runhidden waituntilterminated; Tasks: installservice\startservice
Filename: "{app}\TorrWind.exe"; Description: "Launch TorrWind"; Flags: nowait postinstall skipifsilent

[UninstallRun]
Filename: "{app}\TorrWind.Service.exe"; Parameters: "uninstall"; Flags: runhidden waituntilterminated skipifdoesntexist
