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
- Built-in mpv playback with M3U playlist navigation, audio/video/subtitle controls, and external-player playback.
- Web UI fallback tab for full TorrServer coverage.
- JSON-based localization with runtime language switching.

## MVP

The first usable version should cover:

- Add `.torrent` files.
- Add magnet links.
- Accept `.torrent` paths, `.torrent` HTTP(S) URLs, and `magnet:` links from Windows shell/startup arguments.
- List torrents from selected TorrServer.
- Select a file inside a torrent.
- Open selected torrent file in the built-in mpv player.
- Open TorrServer M3U/M3U8 playlists in the built-in mpv player, show playlist items as episodes, and allow previous/next/direct episode selection.
- Select audio, video, and subtitle tracks in the built-in player, with aspect ratio, audio delay, and subtitle delay controls.
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
- Import and export the full TorrWind settings JSON for profile/provider/player/local-server migration, with a backup of the current settings before import, UI backup listing, restore/deletion, and configurable backup retention.
- Test server connectivity through `GET /echo`.
- Search via TorrServer Torznab endpoint.
- Search directly through configured Torznab-compatible providers such as Jackett and Prowlarr.
- Manage multiple search providers, including URL, API key, categories, enabled flag, timeout, and certificate-error override.
- Filter search by category, seeders, maximum size, and publish-date range.
- Keep a local recent-query search history.
- Show copyable and savable diagnostics for TorrWind version/runtime environment, selected server availability, `/echo`, library count/size, runtime settings, local executable path, and `TorrWindService` state.
- Show DLNA/WebDAV diagnostics, including DLNA runtime state/friendly name and WebDAV endpoint status.
- Export a support bundle containing diagnostics, GUI/service logs, and sanitized settings with secrets redacted.
- Show a combined event log for GUI events, service events, and TorrServer child process stdout/stderr.
- Read, validate, format, copy, and apply the full TorrServer runtime settings JSON through `POST /settings`.
- Check the latest `TorrServer-windows-amd64.exe` release from YouROK/TorrServer, load recent releases with Windows x64 assets, show installed/latest/asset details, and download either latest or the selected release with progress status.
- Verify downloaded TorrServer binaries by SHA256 when GitHub release metadata or checksum assets provide a digest.
- Store downloaded TorrServer binaries in versioned directories.
- Scan locally downloaded TorrServer binaries, switch to a selected local version without network access, and delete inactive local versions.
- Open local data/cache/log/backup/version folders from the GUI.
- Check the latest TorrWind release from `trinity-aml/TorrWind`, download the installer or portable update package into `Data\updates`, verify SHA256 when available, and open the downloaded package.
- Roll back to the previous configured TorrServer binary.
- Optionally start the configured local TorrServer executable together with the GUI when service mode is not selected.
- Install/uninstall `TorrWindService` with UAC elevation only for install/remove operations.
- Start, stop, and query `TorrWindService` from the GUI without elevation.
- English/Russian installer UI text.
- Installer UI for local TorrServer mode choice: GUI-managed process or `TorrWindService`.
- Installer tasks for `.torrent` file association and `magnet:` protocol registration.
- Generate `accs.db` for local HTTP auth.
- Generate `wip.txt` and `bip.txt` for local IP allow/block lists.
- Pass local TorrServer CLI flags for HTTPS, force HTTPS, read-only database, search without auth, max stream size, WebDAV, proxy URL/mode, bind address, and ports.
- Provide file/folder pickers for local executable, data/cache directories, SSL certificate/key files, and custom external player path.
- Apply runtime settings through TorrServer `POST /settings`: cache size, RAM/disk cache mode, disk cache path, download/upload speed limits, DLNA, SSL settings, and TMDB API/image settings.
- Provide field-based runtime settings for advanced TorrServer options including retrackers mode, DHT/peer limits, BT protocol toggles, cache drop/tail behavior, Rutor/Torznab search toggles, JSON storage flags, trusted proxies, and P2P proxy hosts.
- Enable/disable WebDAV through local server launch arguments.
- Keep DLNA as a settings/API item because it is not consistently exposed as a launch argument across TorrServer versions.

## Phase 2

- Signature verification when TorrServer release metadata provides a signature and trust material.

## Data Locations

- Settings: `<TorrWind.exe directory>\Data\settings.json`
- GUI event log: `<TorrWind.exe directory>\Data\logs\gui.jsonl`
- Service event log: `<TorrWind.exe directory>\Data\logs\service.jsonl`
- Local TorrServer binaries/data: `<TorrWind.exe directory>\Data\TorrServer`
- Generated playlists: `<TorrWind.exe directory>\Data\playlists`
- Downloaded TorrWind update packages: `<TorrWind.exe directory>\Data\updates`

TorrWind keeps its settings, logs, backups, playlists, and local TorrServer files in the application working folder. It does not use `%AppData%` or `%ProgramData%` for normal operation.

## Localization

Localization files are flat JSON files:

```text
locales/
  en.json
  ru.json
```

Adding a language means adding another `xx.json` file with the same keys.
