# TorrWind

TorrWind is a Windows 10/11 x64 desktop client for local and remote TorrServer instances.

The goal is to provide a native Windows experience similar to TorrServe: torrent and magnet management, external-player launch, TorrServer lifecycle management, tray mode with quick local-server actions, service installation for a local server, localization, and a fallback Web UI tab for TorrServer features that are not yet native.

## Status

This repository currently contains the initial architecture and MVP scaffold:

- WPF desktop app: `src/TorrWind.App`
- Windows service helper: `src/TorrWind.Service`
- Shared models, API client, settings, localization, update helpers: `src/TorrWind.Core`
- JSON localizations: `locales`
- Windows installer draft: `installers/windows`
- Windows publish/package scripts: `scripts`

Implemented MVP pieces include multi-server profiles, server connection testing, torrent and magnet add, `.torrent`/`magnet:` shell integration, torrent removal, selected torrent details, metadata editing, source/hash copy, drop cache, guarded wipe-all, file selection inside a torrent, playback URL copy, external-player launch, local TorrServer process start/stop and optional GUI startup, service install/uninstall/start/stop/status commands, local auth/IP-list file generation, runtime cache/speed/DLNA settings apply, full runtime settings JSON editing, settings import/export, TorrServer latest-release check, release list loading, selected-version download, rollback to the previous configured TorrServer binary, direct Torznab/Jackett/Prowlarr provider search, search filters, search history, a copyable diagnostics screen for app/server/runtime/service state, and an in-app event log.

## Target Platform

- Windows 10/11 x64
- .NET 8 SDK or newer with Windows desktop workload
- Visual Studio 2022 or `dotnet` CLI on Windows

The project targets WPF because TorrWind is Windows-only and needs tray integration, service management, installer support, and native process control.

## Build

```powershell
dotnet restore
dotnet build
```

In the current Linux sandbox used during development, solution restore must be serialized:

```bash
dotnet build TorrWind.sln -m:1 -p:UseSharedCompilation=false -p:NuGetAudit=false
```

Publish Windows x64 artifacts:

```powershell
.\scripts\publish-win-x64.ps1
```

Create a portable zip:

```powershell
.\scripts\package-win-x64.ps1 -Version 0.1.0
```

Build the Inno Setup installer:

```powershell
.\scripts\build-installer.ps1 -Version 0.1.0
```

On Linux, the same script can build the installer through PowerShell + Wine when Inno Setup is installed in a Wine prefix:

```bash
pwsh ./scripts/build-installer.ps1 -Version 0.1.0
```

By default the script checks `~/.wine-inno` and `~/.wine`. Custom paths can be passed with `-WinePrefix` or `-InnoCompilerPath`.

Build all release artifacts and checksums:

```powershell
.\scripts\release-win-x64.ps1 -Version 0.1.0
```

The installer supports English and Russian UI text. It can create a desktop icon, enable Windows startup, associate `.torrent` files, register the `magnet:` protocol handler, install `TorrWindService`, and optionally start the service after installation.

`TorrWind.exe` accepts startup arguments for shell integration:

```powershell
.\TorrWind.exe --minimized
.\TorrWind.exe "C:\Downloads\movie.torrent"
.\TorrWind.exe "magnet:?xt=urn:btih:..."
.\TorrWind.exe "https://example.org/file.torrent"
```

Torrent arguments are added to the currently selected writable server profile. TorrWind runs as a single GUI instance; if it is already running, a later `.torrent` or `magnet:` activation is forwarded to the existing process and the existing window is restored.

The service helper also supports direct commands:

```powershell
.\TorrWind.Service.exe install
.\TorrWind.Service.exe start
.\TorrWind.Service.exe stop
.\TorrWind.Service.exe uninstall
```

The GUI exposes the same service lifecycle controls. Install/uninstall request UAC; start/stop/status use normal `sc.exe` calls and report permission errors in the status bar/log.

When local server startup is enabled in settings, the GUI starts the configured TorrServer executable on launch. This is skipped when service mode is selected or no executable has been configured yet.

The settings screen provides file/folder pickers for the TorrServer executable, data/cache folders, SSL certificate/key files, and a custom external player.

The settings screen can export the full TorrWind settings JSON and import it back later. This includes server profiles, local TorrServer settings, external-player settings, search providers, search history, and selected language. Import asks for confirmation because it replaces the current configuration, and TorrWind writes a timestamped backup to `%AppData%\TorrWind\backups` before importing. Existing backups are listed in the same settings screen and can be refreshed, restored, deleted, or selected manually from disk. A retention limit controls how many recent backup files are kept; `0` keeps all backups.

## TorrServer Integration

TorrWind uses the public TorrServer API:

- `GET /echo` for health/version
- `POST /torrents` for list/add/remove/get/set/drop/wipe
- `POST /torrent/upload` for `.torrent` uploads
- `GET /play/{hash}/{id}` and `GET /stream` for playback URLs
- `GET /torznab/search` for Torznab search
- `POST /settings` for server settings

TorrServer Swagger is available from a running server at `/swagger/index.html`.

The library screen uses `/torrents` actions `list`, `get`, `set`, `rem`, `drop`, and `wipe`. `wipe` is guarded by a confirmation dialog because it removes all torrents from the selected server.

The Runtime JSON screen reads the full `POST /settings` object, formats it for editing, validates JSON locally, and applies it back through `action=set`. Server profiles marked read-only cannot apply edited runtime JSON.

The search screen can use either the selected TorrServer search endpoint or configured Torznab-compatible providers. Jackett and Prowlarr can be added through their Torznab feed URLs with an optional API key, category list, timeout, and certificate-error override.

The diagnostics screen checks the TorrWind version and runtime environment, selected profile, `/echo`, torrent library count/size, selected runtime settings, and local service state when the profile is marked as local. The generated report can be copied to the clipboard.

The log screen reads GUI events from `%AppData%\TorrWind\logs\gui.jsonl` and service events from `%ProgramData%\TorrWind\logs\service.jsonl`. Local and service TorrServer stdout/stderr are captured into those logs when TorrWind starts the process.

## License

GPL-3.0-only. See `LICENSE`.
