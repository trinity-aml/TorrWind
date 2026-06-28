# TorrWind Product Specification

## Scope

TorrWind is a GPL-3.0 Windows 10/11 x64 desktop application inspired by TorrServe.

It provides:

- Native GUI for local and remote TorrServer profiles.
- Tray mode.
- Tray menu for opening the app/Web UI and starting/stopping the local TorrServer process.
- Single-instance GUI behavior with argument handoff from secondary launches.
- Local TorrServer download/update from GitHub Releases.
- Local TorrServer execution either as a foreground child process or as `TorrWindService`.
- External-player playback for MVP.
- Web UI fallback tab for full TorrServer coverage.
- JSON-based localization with runtime language switching.

## MVP

The first usable version should cover:

- Add `.torrent` files.
- Add magnet links.
- Accept `.torrent` paths, `.torrent` HTTP(S) URLs, and `magnet:` links from Windows shell/startup arguments.
- List torrents from selected TorrServer.
- Select a file inside a torrent.
- Open selected torrent file in external player.
- Copy selected playback URL.
- Remove torrents from writable server profiles.
- Refresh selected torrent details through `/torrents` `action=get`.
- Edit selected torrent title, poster, category, and data through `/torrents` `action=set`.
- Copy selected torrent source/hash.
- Drop selected torrent from active cache through `/torrents` `action=drop`.
- Wipe all torrents on the selected server through `/torrents` `action=wipe` only after confirmation.
- Manage multiple server profiles.
- Store profile URL, auth, SSL ignore option, local/read-only flags.
- Test server connectivity through `GET /echo`.
- Search via TorrServer Torznab endpoint.
- Search directly through configured Torznab-compatible providers such as Jackett and Prowlarr.
- Manage multiple search providers, including URL, API key, categories, enabled flag, timeout, and certificate-error override.
- Filter search by category, seeders, and maximum size.
- Keep a local recent-query search history.
- Show diagnostics for selected server availability, `/echo`, library count/size, runtime settings, local executable path, and `TorrWindService` state.
- Show a combined event log for GUI events, service events, and TorrServer child process stdout/stderr.
- Read, validate, format, copy, and apply the full TorrServer runtime settings JSON through `POST /settings`.
- Download latest `TorrServer-windows-amd64.exe` from YouROK/TorrServer releases.
- Store downloaded TorrServer binaries in versioned directories.
- Roll back to the previous configured TorrServer binary.
- Install/uninstall `TorrWindService` with UAC elevation only for install/remove operations.
- Start, stop, and query `TorrWindService` from the GUI without elevation.
- Installer tasks for `.torrent` file association and `magnet:` protocol registration.
- Generate `accs.db` for local HTTP auth.
- Generate `wip.txt` and `bip.txt` for local IP allow/block lists.
- Pass local TorrServer CLI flags for HTTPS, force HTTPS, read-only database, search without auth, max stream size, WebDAV, proxy URL/mode, bind address, and ports.
- Apply runtime settings through TorrServer `POST /settings`: cache size, RAM/disk cache mode, disk cache path, download/upload speed limits, DLNA, and SSL settings.
- Enable/disable WebDAV through local server launch arguments.
- Keep DLNA as a settings/API item because it is not consistently exposed as a launch argument across TorrServer versions.

## Phase 2

- Built-in LibVLC player.
- Installer UI for service choice.
- Self-update for TorrWind.
- Checksum/signature verification when TorrServer release metadata provides it.
- Field-based editor for more TorrServer settings.
- Deeper DLNA/WebDAV diagnostics.
- Publish-date range filtering.

## Data Locations

- User settings: `%AppData%\TorrWind\settings.json`
- GUI event log: `%AppData%\TorrWind\logs\gui.jsonl`
- Service settings: `%ProgramData%\TorrWind\settings.json`
- Service event log: `%ProgramData%\TorrWind\logs\service.jsonl`
- Local TorrServer binaries/data: `%ProgramData%\TorrWind\TorrServer`

The app attempts to copy settings to `%ProgramData%` for the service. If Windows permissions reject that write, the user-facing settings are still saved under `%AppData%`.

## Localization

Localization files are flat JSON files:

```text
locales/
  en.json
  ru.json
```

Adding a language means adding another `xx.json` file with the same keys.
