#define AppName "TorrWind"
#ifndef AppVersion
#define AppVersion "1.0.3"
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

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"

[CustomMessages]
english.AdditionalIconsGroup=Additional icons:
russian.AdditionalIconsGroup=Дополнительные значки:
english.StartupGroup=Startup:
russian.StartupGroup=Автозапуск:
english.WindowsIntegrationGroup=Windows integration:
russian.WindowsIntegrationGroup=Интеграция Windows:
english.WindowsServiceGroup=Windows service:
russian.WindowsServiceGroup=Служба Windows:
english.LocalServerModeGroup=Local TorrServer mode:
russian.LocalServerModeGroup=Режим локального TorrServer:
english.DesktopIconTask=Create a desktop icon
russian.DesktopIconTask=Создать значок на рабочем столе
english.StartupTask=Start TorrWind with Windows
russian.StartupTask=Запускать TorrWind вместе с Windows
english.FileAssociationTask=Associate .torrent files with TorrWind
russian.FileAssociationTask=Связать .torrent файлы с TorrWind
english.MagnetAssociationTask=Register TorrWind as the magnet link handler
russian.MagnetAssociationTask=Зарегистрировать TorrWind для magnet-ссылок
english.GuiModeTask=Use GUI-managed local TorrServer
russian.GuiModeTask=Использовать локальный TorrServer под управлением GUI
english.ServiceModeTask=Install TorrWindService for local TorrServer
russian.ServiceModeTask=Установить TorrWindService для локального TorrServer
english.StartServiceTask=Start TorrWindService after installation
russian.StartServiceTask=Запустить TorrWindService после установки
english.InstallingServiceStatus=Installing TorrWindService...
russian.InstallingServiceStatus=Установка TorrWindService...
english.StartingServiceStatus=Starting TorrWindService...
russian.StartingServiceStatus=Запуск TorrWindService...
english.LaunchApp=Launch TorrWind
russian.LaunchApp=Запустить TorrWind
english.TorrentFileType=Torrent file
russian.TorrentFileType=Torrent-файл

[Dirs]
Name: "{app}\Data"; Permissions: users-modify
Name: "{app}\Data\TorrServer"; Permissions: users-modify
Name: "{app}\Data\logs"; Permissions: users-modify
Name: "{app}\Data\backups"; Permissions: users-modify
Name: "{app}\Data\playlists"; Permissions: users-modify
Name: "{app}\Data\WebView2"; Permissions: users-modify

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\TorrWind"; Filename: "{app}\TorrWind.exe"
Name: "{autodesktop}\TorrWind"; Filename: "{app}\TorrWind.exe"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "{cm:DesktopIconTask}"; GroupDescription: "{cm:AdditionalIconsGroup}"
Name: "startup"; Description: "{cm:StartupTask}"; GroupDescription: "{cm:StartupGroup}"
Name: "fileassoc"; Description: "{cm:FileAssociationTask}"; GroupDescription: "{cm:WindowsIntegrationGroup}"
Name: "magnetassoc"; Description: "{cm:MagnetAssociationTask}"; GroupDescription: "{cm:WindowsIntegrationGroup}"
Name: "guimode"; Description: "{cm:GuiModeTask}"; GroupDescription: "{cm:LocalServerModeGroup}"; Flags: exclusive
Name: "servicemode"; Description: "{cm:ServiceModeTask}"; GroupDescription: "{cm:LocalServerModeGroup}"; Flags: exclusive unchecked
Name: "servicemode\startservice"; Description: "{cm:StartServiceTask}"; Flags: unchecked

[Registry]
Root: HKLM; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "TorrWind"; ValueData: """{app}\TorrWind.exe"" --minimized"; Flags: uninsdeletevalue; Tasks: startup
Root: HKCR; Subkey: ".torrent"; ValueType: string; ValueName: ""; ValueData: "TorrWind.Torrent"; Flags: uninsdeletevalue; Tasks: fileassoc
Root: HKCR; Subkey: "TorrWind.Torrent"; ValueType: string; ValueName: ""; ValueData: "{cm:TorrentFileType}"; Flags: uninsdeletekey; Tasks: fileassoc
Root: HKCR; Subkey: "TorrWind.Torrent\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: "{app}\TorrWind.exe,0"; Tasks: fileassoc
Root: HKCR; Subkey: "TorrWind.Torrent\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\TorrWind.exe"" ""%1"""; Tasks: fileassoc
Root: HKCR; Subkey: "magnet"; ValueType: string; ValueName: ""; ValueData: "URL:Magnet Protocol"; Flags: uninsdeletekey; Tasks: magnetassoc
Root: HKCR; Subkey: "magnet"; ValueType: string; ValueName: "URL Protocol"; ValueData: ""; Tasks: magnetassoc
Root: HKCR; Subkey: "magnet\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: "{app}\TorrWind.exe,0"; Tasks: magnetassoc
Root: HKCR; Subkey: "magnet\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\TorrWind.exe"" ""%1"""; Tasks: magnetassoc

[Run]
Filename: "{app}\TorrWind.Service.exe"; Parameters: "install"; StatusMsg: "{cm:InstallingServiceStatus}"; Flags: runhidden waituntilterminated; Tasks: servicemode
Filename: "{app}\TorrWind.Service.exe"; Parameters: "start"; StatusMsg: "{cm:StartingServiceStatus}"; Flags: runhidden waituntilterminated; Tasks: servicemode\startservice
Filename: "{app}\TorrWind.exe"; Description: "{cm:LaunchApp}"; Flags: nowait postinstall skipifsilent

[UninstallRun]
Filename: "{app}\TorrWind.Service.exe"; Parameters: "uninstall"; Flags: runhidden waituntilterminated skipifdoesntexist; RunOnceId: "UninstallTorrWindService"
