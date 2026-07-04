# Build on Windows

## Prerequisites

- Windows 10/11 x64
- .NET 8 SDK or newer
- Visual Studio 2022 with .NET desktop development workload, or `dotnet` CLI
- 7-Zip/p7zip for bundling the mpv runtime
- Inno Setup 6 if building the installer

## Build

```powershell
dotnet restore .\TorrWind.sln
dotnet build .\TorrWind.sln -c Debug
```

If a Linux development environment shows a silent solution-level restore failure with .NET 10, use serialized build:

```bash
dotnet build TorrWind.sln -m:1 -p:UseSharedCompilation=false -p:NuGetAudit=false
```

## Publish

```powershell
.\scripts\publish-win-x64.ps1
```

This publishes `TorrWind.exe` and `TorrWind.Service.exe` into `artifacts\publish\TorrWind`. It also downloads the latest shinchiro Windows x64 mpv runtime, verifies the SHA256 digest from the GitHub release metadata when present, and installs it into `artifacts\publish\TorrWind\Runtime\mpv`.

Offline or custom mpv runtime:

```powershell
.\scripts\publish-win-x64.ps1 -MpvRuntimeArchivePath .\mpv-x86_64-YYYYMMDD-git-xxxxxxx.7z
```

Skip bundling mpv for a local publish:

```powershell
.\scripts\publish-win-x64.ps1 -SkipMpvRuntime
```

Create a portable zip:

```powershell
.\scripts\package-win-x64.ps1 -Version 1.0.0
```

## Installer

Install Inno Setup 6 and run:

```powershell
.\scripts\build-installer.ps1 -Version 1.0.0
```

The installer output is written to `artifacts\installer`.

Installer tasks:

- Desktop icon.
- Start TorrWind with Windows using `TorrWind.exe --minimized`.
- Install `TorrWindService`.
- Optionally start `TorrWindService` after installation.

The installer also grants normal users modify access to `{app}\Data`, so the GUI and service can share settings, logs, backups, playlists, and downloaded TorrServer binaries inside the install directory.

Build all release artifacts and SHA256 checksums:

```powershell
.\scripts\release-win-x64.ps1 -Version 1.0.0
```

Run unit tests:

```powershell
dotnet test TorrWind.sln
```

On Linux build machines with only a newer .NET runtime installed, use `DOTNET_ROLL_FORWARD=Major dotnet test TorrWind.sln` or install the .NET 8 runtime.

## Service Commands

From the published directory:

```powershell
.\TorrWind.Service.exe install
.\TorrWind.Service.exe start
.\TorrWind.Service.exe stop
.\TorrWind.Service.exe uninstall
```
