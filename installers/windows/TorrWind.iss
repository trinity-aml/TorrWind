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
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=admin
LicenseFile=..\..\LICENSE
SetupIconFile=..\..\assets\TorrWind.ico
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
Name: "fileassoc"; Description: "Associate .torrent files with TorrWind"; GroupDescription: "Windows integration:"
Name: "magnetassoc"; Description: "Register TorrWind as the magnet link handler"; GroupDescription: "Windows integration:"
Name: "installservice"; Description: "Install TorrWindService for local TorrServer"; GroupDescription: "Windows service:"
Name: "installservice\startservice"; Description: "Start TorrWindService after installation"; Flags: unchecked

[Registry]
Root: HKLM; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "TorrWind"; ValueData: """{app}\TorrWind.exe"" --minimized"; Flags: uninsdeletevalue; Tasks: startup
Root: HKCR; Subkey: ".torrent"; ValueType: string; ValueName: ""; ValueData: "TorrWind.Torrent"; Flags: uninsdeletevalue; Tasks: fileassoc
Root: HKCR; Subkey: "TorrWind.Torrent"; ValueType: string; ValueName: ""; ValueData: "Torrent file"; Flags: uninsdeletekey; Tasks: fileassoc
Root: HKCR; Subkey: "TorrWind.Torrent\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: "{app}\TorrWind.exe,0"; Tasks: fileassoc
Root: HKCR; Subkey: "TorrWind.Torrent\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\TorrWind.exe"" ""%1"""; Tasks: fileassoc
Root: HKCR; Subkey: "magnet"; ValueType: string; ValueName: ""; ValueData: "URL:Magnet Protocol"; Flags: uninsdeletekey; Tasks: magnetassoc
Root: HKCR; Subkey: "magnet"; ValueType: string; ValueName: "URL Protocol"; ValueData: ""; Tasks: magnetassoc
Root: HKCR; Subkey: "magnet\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: "{app}\TorrWind.exe,0"; Tasks: magnetassoc
Root: HKCR; Subkey: "magnet\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\TorrWind.exe"" ""%1"""; Tasks: magnetassoc

[Run]
Filename: "{app}\TorrWind.Service.exe"; Parameters: "install"; StatusMsg: "Installing TorrWindService..."; Flags: runhidden waituntilterminated; Tasks: installservice
Filename: "{app}\TorrWind.Service.exe"; Parameters: "start"; StatusMsg: "Starting TorrWindService..."; Flags: runhidden waituntilterminated; Tasks: installservice\startservice
Filename: "{app}\TorrWind.exe"; Description: "Launch TorrWind"; Flags: nowait postinstall skipifsilent

[UninstallRun]
Filename: "{app}\TorrWind.Service.exe"; Parameters: "uninstall"; Flags: runhidden waituntilterminated skipifdoesntexist; RunOnceId: "UninstallTorrWindService"
