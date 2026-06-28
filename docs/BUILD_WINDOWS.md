# Build on Windows

## Prerequisites

- Windows 10/11 x64
- .NET 8 SDK or newer
- Visual Studio 2022 with .NET desktop development workload, or `dotnet` CLI
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

This publishes `TorrWind.exe` and `TorrWind.Service.exe` into `artifacts\publish\TorrWind`.

Create a portable zip:

```powershell
.\scripts\package-win-x64.ps1 -Version 0.1.0
```

## Installer

Install Inno Setup 6 and run:

```powershell
.\scripts\build-installer.ps1 -Version 0.1.0
```

The installer output is written to `artifacts\installer`.

Installer tasks:

- Desktop icon.
- Start TorrWind with Windows using `TorrWind.exe --minimized`.
- Install `TorrWindService`.
- Optionally start `TorrWindService` after installation.

The installer also grants normal users modify access to `%ProgramData%\TorrWind`, so the GUI can write shared settings for the service.

## Service Commands

From the published directory:

```powershell
.\TorrWind.Service.exe install
.\TorrWind.Service.exe start
.\TorrWind.Service.exe stop
.\TorrWind.Service.exe uninstall
```
