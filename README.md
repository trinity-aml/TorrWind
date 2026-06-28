# TorrWind

TorrWind is a Windows 10/11 x64 desktop client for local and remote TorrServer instances.

The goal is to provide a native Windows experience similar to TorrServe: torrent and magnet management, external-player launch, TorrServer lifecycle management, tray mode, service installation for a local server, localization, and a fallback Web UI tab for TorrServer features that are not yet native.

## Status

This repository currently contains the initial architecture and MVP scaffold:

- WPF desktop app: `src/TorrWind.App`
- Windows service helper: `src/TorrWind.Service`
- Shared models, API client, settings, localization, update helpers: `src/TorrWind.Core`
- JSON localizations: `locales`
- Windows installer draft: `installers/windows`
- Windows publish/package scripts: `scripts`

Implemented MVP pieces include multi-server profiles, server connection testing, torrent and magnet add, torrent removal, file selection inside a torrent, playback URL copy, external-player launch, local TorrServer process start/stop, service install/uninstall commands, local auth/IP-list file generation, runtime cache/speed/DLNA settings apply, versioned TorrServer download, rollback to the previous configured TorrServer binary, direct Torznab/Jackett/Prowlarr provider search, search filters, search history, a diagnostics screen for server/runtime/service state, and an in-app event log.

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

The installer can create a desktop icon, enable Windows startup, install `TorrWindService`, and optionally start the service after installation.

The service helper also supports direct commands:

```powershell
.\TorrWind.Service.exe install
.\TorrWind.Service.exe start
.\TorrWind.Service.exe stop
.\TorrWind.Service.exe uninstall
```

## TorrServer Integration

TorrWind uses the public TorrServer API:

- `GET /echo` for health/version
- `POST /torrents` for list/add/remove/get/set/drop/wipe
- `POST /torrent/upload` for `.torrent` uploads
- `GET /play/{hash}/{id}` and `GET /stream` for playback URLs
- `GET /torznab/search` for Torznab search
- `POST /settings` for server settings

TorrServer Swagger is available from a running server at `/swagger/index.html`.

The search screen can use either the selected TorrServer search endpoint or configured Torznab-compatible providers. Jackett and Prowlarr can be added through their Torznab feed URLs with an optional API key, category list, timeout, and certificate-error override.

The diagnostics screen checks the selected profile, `/echo`, torrent library count/size, selected runtime settings, and local service state when the profile is marked as local.

The log screen reads GUI events from `%AppData%\TorrWind\logs\gui.jsonl` and service events from `%ProgramData%\TorrWind\logs\service.jsonl`. Local and service TorrServer stdout/stderr are captured into those logs when TorrWind starts the process.

## License

GPL-3.0-only. See `LICENSE`.
