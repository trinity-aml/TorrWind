# TorrWind 1.0.0

Languages: English | [Русский](README.ru.md)

Beginner guide: [Русский](docs/BEGINNER_GUIDE_RU.md)

TorrWind is a Windows 10/11 x64 desktop client for local and remote TorrServer instances.

Repository: https://github.com/trinity-aml/TorrWind  
License: GPL-3.0-only

TorrWind keeps its settings, logs, downloaded TorrServer binaries, playlists, backups, and other runtime files in the application working directory under `Data`. It does not use `%ProgramData%` or `%AppData%` for normal portable operation.

## Features

- Local and remote TorrServer profile management.
- Local TorrServer download/update from GitHub releases.
- Local TorrServer process control and optional Windows Service mode through `TorrWind.Service.exe`.
- Torrent and magnet add/remove/drop/wipe actions.
- Torrent file list, selected-file playback, continue playlist, and playlist-from-current-file actions.
- Built-in mpv player with M3U playlist navigation, audio/video/subtitle controls, and external-player launch.
- TorrServer Web UI fallback tab.
- Torznab-compatible indexer search, including Jackett/Prowlarr style endpoints.
- Runtime JSON editor for TorrServer settings.
- Cache settings with memory or disk mode. New profiles default to 64 MB memory cache.
- Diagnostics report, GUI/service logs, settings import/export, and support bundle export.
- JSON localization files in `locales`.

## Requirements

Runtime target:

- Windows 10/11 x64
- .NET 8 desktop runtime is bundled in self-contained release builds

Development on Windows:

- .NET 8 SDK
- PowerShell 7 or Windows PowerShell
- Inno Setup 6 for installer builds

Development on Linux:

- .NET 8 SDK
- PowerShell 7
- Wine + Inno Setup 6 in a Wine prefix for installer builds

## Build On Windows

Restore and build:

```powershell
dotnet restore
dotnet build TorrWind.sln
```

Publish self-contained Windows x64 files:

```powershell
.\scripts\publish-win-x64.ps1 -Version 1.0.0
```

The publish script downloads the latest shinchiro Windows x64 mpv runtime, verifies the GitHub release SHA256 digest when present, and installs it into `artifacts/publish/TorrWind/Runtime/mpv`. Install 7-Zip before release packaging. For offline builds, pass `-MpvRuntimeArchivePath <mpv-x86_64-...7z>`; to publish without bundled mpv, pass `-SkipMpvRuntime`.

Create a portable zip:

```powershell
.\scripts\package-win-x64.ps1 -Version 1.0.0
```

Build the Inno Setup installer:

```powershell
.\scripts\build-installer.ps1 -Version 1.0.0
```

Build all release artifacts and checksums:

```powershell
.\scripts\release-win-x64.ps1 -Version 1.0.0
```

Outputs are written to:

- `artifacts/publish/TorrWind`
- `artifacts/portable/TorrWind-1.0.0-win-x64-portable.zip`
- `artifacts/installer/TorrWind-1.0.0-win-x64.exe`
- `artifacts/TorrWind-1.0.0-SHA256SUMS.txt`

## Build On Linux

TorrWind is a Windows desktop app, but the repository can be built and packaged from Linux with Windows targeting enabled.

Build the solution:

```bash
DOTNET_CLI_HOME="$PWD/.dotnet" \
NUGET_PACKAGES="$PWD/.nuget/packages" \
DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 \
dotnet build TorrWind.sln -m:1 -p:UseSharedCompilation=false -p:NuGetAudit=false
```

Create a portable Windows x64 zip:

```bash
pwsh ./scripts/package-win-x64.ps1 -Version 1.0.0
```

The packaging scripts also bundle mpv through `scripts/install-mpv-runtime.ps1`. Install `p7zip`/`7z` in Linux build environments, or pass `-MpvRuntimeArchivePath <mpv-x86_64-...7z>` for offline builds.

Build the installer through Wine + Inno Setup:

```bash
pwsh ./scripts/build-installer.ps1 -Version 1.0.0
```

The installer script searches `~/.wine-inno` and `~/.wine` by default. You can override detection:

```bash
pwsh ./scripts/build-installer.ps1 \
  -Version 1.0.0 \
  -WinePrefix "$HOME/.wine-inno" \
  -InnoCompilerPath "$HOME/.wine-inno/drive_c/InnoSetup6/ISCC.exe"
```

Build all release artifacts:

```bash
pwsh ./scripts/release-win-x64.ps1 -Version 1.0.0
```

## Release Workflow

GitHub Actions workflow: `.github/workflows/release.yml`.

It runs automatically when a tag is pushed:

```bash
git tag v1.0.0
git push origin v1.0.0
```

It can also be started manually from GitHub Actions with a version input. The workflow:

- installs .NET 8 and Inno Setup on `windows-latest`;
- runs `scripts/release-win-x64.ps1`;
- uploads the installer, portable zip, and SHA256SUMS as workflow artifacts;
- creates or updates a GitHub Release with the same files.

## First Setup

1. Download the portable zip or installer from a release.
2. Start `TorrWind.exe`.
3. Open `Settings -> TorrServer`.
4. Click `Check latest`, `Load releases`, or `Download TorrServer` to download the local TorrServer binary.
5. Use `Start local` for GUI-managed local TorrServer, or use the `Service` settings to install/start `TorrWindService`.
6. Open `Library` and add a `.torrent`, magnet link, or search result.
7. Select a torrent file and use `Open player`, `Continue`, or `Playlist from selected`.

## Local TorrServer

Local TorrServer files are stored under:

```text
Data/TorrServer
Data/TorrServer/versions
Data/TorrServer/cache
```

The GUI can:

- download/update TorrServer from `YouROK/TorrServer` GitHub releases;
- switch between downloaded local versions;
- start/stop TorrServer as a child process;
- install/uninstall/start/stop/query `TorrWindService`;
- apply TorrServer runtime settings from the native settings screen or Runtime JSON tab, including TMDB API and image URL settings.

Service install/uninstall/start/stop can request elevation. Normal settings editing and remote-server use do not require administrator rights.

## Remote TorrServer

Open `Settings -> Servers` and add a profile:

- name;
- base URL such as `http://192.168.1.2:8090`;
- optional username/password;
- optional `ignore certificate errors`;
- read-only mode when you do not want TorrWind to modify the server.

Remote servers can be used for library actions, playback URL generation, Web UI, diagnostics, and search through the selected TorrServer.

## Search Indexers

Open `Settings -> Indexers` and add Torznab-compatible providers.

Typical URLs:

```text
http://127.0.0.1:9117/api/v2.0/indexers/all/results/torznab
http://192.168.1.2:9696/api/v1/indexer/all/results/torznab
http://192.168.1.2:5002
```

TorrWind normalizes common Jackett/Prowlarr/JacPro-style URLs, supports API keys, category filters, timeouts, and certificate-error override.

## Playback

Playback can use the built-in mpv player or an external player:

- built-in mpv;
- system default player;
- VLC;
- MPC-HC;
- PotPlayer;
- custom executable path.

TorrWind generates TorrServer-compatible stream or M3U URLs and opens them in the selected player. Release builds bundle the Windows x64 mpv runtime under `Runtime\mpv`; TorrWind also looks for `mpv.exe` in the application folder, `mpv`, `tools\mpv`, and then `PATH`.

The built-in mpv player reads local M3U files and downloads HTTP(S) M3U/M3U8 playlists itself. Series playlists are shown as an episode list, with icon controls for previous/next episode and direct selection of any playlist item. The player also exposes audio track, video track, subtitle track, aspect ratio, audio delay, and subtitle delay controls.

## Cache And Runtime Settings

New local profiles default to:

- memory cache mode;
- 64 MB cache size;
- 50% preload buffer;
- 95% read-ahead cache;
- 30 second torrent disconnect timeout;
- 25 torrent connections.

Cache can be switched to disk mode and pointed at a folder under `Data/TorrServer/cache` or another user-selected path.

## Diagnostics And Logs

Diagnostics can be copied, saved, or packed into a support bundle. Sensitive values such as passwords, API keys, tokens, and secrets are sanitized.

Logs are stored in:

```text
Data/logs/gui.jsonl
Data/logs/service.jsonl
```

When TorrWind starts local TorrServer, stdout/stderr are captured into the same log system.

## Shell Integration

The installer can:

- create a desktop icon;
- start TorrWind with Windows;
- associate `.torrent` files;
- register `magnet:` links;
- install and optionally start `TorrWindService`.

`TorrWind.exe` accepts:

```powershell
.\TorrWind.exe --minimized
.\TorrWind.exe "C:\Downloads\movie.torrent"
.\TorrWind.exe "magnet:?xt=urn:btih:..."
.\TorrWind.exe "https://example.org/file.torrent"
```

TorrWind runs as a single GUI instance. Later shell activations are forwarded to the existing instance.

## License

GPL-3.0-only. See `LICENSE`.
