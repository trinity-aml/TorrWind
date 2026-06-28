# TorrWind Architecture

## Projects

`TorrWind.App`
: WPF desktop application, tray integration, native UI, Web UI tab, external-player launch, and profile management.

`TorrWind.Service`
: Windows service helper. It supervises the configured local `TorrServer.exe` process and exposes `install`, `uninstall`, `start`, and `stop` commands for installers and manual administration.

`TorrWind.Core`
: Shared settings, models, localization, TorrServer API client, Torznab provider client, release downloader, file event log, local process launcher, external-player launcher, Windows service helper commands.

## Runtime Modes

### Remote Server

The GUI talks directly to a configured TorrServer URL. Auth and certificate behavior are stored per server profile.

### Local Server, GUI Mode

Planned mode where the GUI starts `TorrServer.exe` as a child process when the app starts.

### Local Server, Service Mode

`TorrWindService` reads `%ProgramData%\TorrWind\settings.json`, builds TorrServer arguments, starts the configured TorrServer executable, and restarts it after unexpected exit.

## API Layer

`TorrServerClient` wraps TorrServer endpoints used by the MVP:

- `GET /echo`
- `POST /torrents`
- `POST /torrent/upload`
- `GET /play/{hash}/{id}`
- `GET /stream`
- `GET /torznab/search`
- `GET /settings`

The parser is intentionally tolerant because TorrServer JSON field names differ between API objects and web UI payloads.

The library UI uses the full `/torrents` action surface needed by the MVP: `list`, `get`, `set`, `rem`, `drop`, and guarded `wipe`. Selected torrent details are normalized into `TorrentItem`, including category, poster, custom data, size/loaded/preloaded values, peers, seeders, and transfer speeds.

`TorznabSearchClient` handles direct search against configured Torznab-compatible providers. Provider settings are stored in `%AppData%\TorrWind\settings.json` with name, URL, API key, default categories, enabled state, timeout, and certificate behavior. The UI can search the selected TorrServer, one configured provider, or all enabled providers; results are normalized into `SearchResult` and then filtered by seed count, maximum size, and category.

The diagnostics tab composes existing API calls instead of keeping a background monitor. It checks `/echo`, torrent list count/size, selected `POST /settings` fields, and local executable/service state for profiles marked as local.

The Runtime JSON tab exposes the full settings object returned by `POST /settings` with `action=get`. It validates that the edited JSON root is an object, formats it with indentation, and sends it back through `action=set`. This gives immediate coverage for TorrServer options that do not yet have dedicated UI controls.

## Event Logging

`FileEventLog` writes JSONL records with timestamp, level, source, message, details, exception text, and source log file. GUI events are stored under `%AppData%\TorrWind\logs\gui.jsonl`; service events are stored under `%ProgramData%\TorrWind\logs\service.jsonl`. Logs rotate to `.1` at roughly 2 MB.

The log tab reads both files, sorts the latest entries by timestamp, and lets the user refresh, copy log paths, or clear the GUI log. The service log is not cleared from the GUI because it may require elevated permissions and is more useful for post-failure analysis.

## TorrServer Updates

Downloaded TorrServer binaries are stored under:

```text
%ProgramData%\TorrWind\TorrServer\versions\<version>\
```

Before switching to a newly downloaded binary, TorrWind keeps the previous executable path and version in settings. The rollback command swaps the current and previous executable values.

## Local Server Configuration

Before starting a local TorrServer process or service child process, TorrWind writes files into the configured TorrServer data directory:

- `accs.db` for HTTP Basic auth accounts.
- `wip.txt` for IP whitelist entries.
- `bip.txt` for IP blacklist entries.

The launch argument builder maps supported local settings to TorrServer flags such as `--httpauth`, `--ssl`, `--sslport`, `--sslcert`, `--sslkey`, `--force-https`, `--rdb`, `--searchwa`, `--maxsize`, `--webdav`, `--proxyurl`, and `--proxymode`.

Runtime settings that live in TorrServer's settings database are applied through `POST /settings` with `action=set`, preserving the existing settings object and changing only TorrWind-owned fields.

## Service Management

`WindowsServiceManager` shells out to `sc.exe`. Install and uninstall use `Verb = runas`; start, stop, and status query run without elevation so UAC is limited to service creation/removal.

The settings screen exposes service install/uninstall/start/stop/status. Before service start, the GUI saves current settings so the service can read `%ProgramData%\TorrWind\settings.json`.

## Installer

The Inno Setup script installs the GUI and service helper into `Program Files`. It can optionally install/start `TorrWindService`, create a desktop icon, register `TorrWind.exe --minimized` for Windows startup, associate `.torrent` files, and register TorrWind as a `magnet:` protocol handler.

Shell activation is handled in the GUI startup path. `TorrWind.exe` ignores control arguments such as `--minimized` and adds supported `.torrent` paths, `.torrent` HTTP(S) URLs, `magnet:`, and `torrs://` links to the currently selected writable server profile.

The GUI is single-instance. A named mutex elects the primary process, and secondary launches forward their startup arguments over a named pipe before exiting. The primary process restores its window and processes forwarded torrent arguments on the UI dispatcher.

Portable mode is the published folder or portable zip without service installation. Service setup remains optional.
