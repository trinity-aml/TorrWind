# TorrWind: Beginner Guide

Languages: English | [Русский](BEGINNER_GUIDE_RU.md)

This guide covers normal installation, first-time setup, and basic use of TorrWind on Windows 10/11 x64.

## What TorrWind Is

TorrWind is a desktop application for managing TorrServer. It can work in two modes:

- `Local TorrServer`: TorrWind downloads, starts, and configures TorrServer on the same computer.
- `Remote TorrServer`: TorrWind connects to an already running TorrServer by IP address or domain name.

For a first setup, the local mode is usually easier.

## What To Download

A release normally contains two variants:

- `portable zip`: extract it to any folder and run it without installation.
- `installer exe`: install TorrWind through a normal Windows installer.

For beginners, `portable zip` is usually simpler. The working folder, settings, logs, playlists, TorrServer, and cache will stay next to `TorrWind.exe` under the `Data` folder.

## Installing The Portable Version

1. Download `TorrWind-...-win-x64-portable.zip`.
2. Extract the archive to a permanent folder, for example:

   ```text
   D:\Apps\TorrWind
   ```

3. Run `TorrWind.exe`.
4. If Windows SmartScreen shows a warning, make sure the file came from your TorrWind release, then choose `More info -> Run anyway`.

Do not run the portable version directly from the archive. Extract it first.

## Updating TorrWind

1. Open `Settings -> Service`.
2. Click `Check TorrWind update`.
3. If a newer release is available, click `Download TorrWind update`.
4. Click `Open downloaded update`.

TorrWind stores downloaded update packages under `Data\updates`. The app verifies SHA256 when the release provides a checksum. For portable builds, close TorrWind before replacing files from the new portable zip.

## First Local TorrServer Setup

1. Open `Settings -> TorrServer`.
2. Click `Check latest` or `Load releases`.
3. Click `Download TorrServer`.
4. After the download completes, click `Save`.
5. Click `Start local`.
6. Open `Diagnostics -> Diagnostics` and click `Run diagnostics`.
7. Make sure the selected server is online.

After that, TorrWind is ready to add torrents and start playback.

## Running The Local Server As A Windows Service

Use the service mode if TorrServer should run without an open TorrWind window or start with Windows.

1. Open `Settings -> Service`.
2. Enable local server service mode if that switch is available.
3. Click `Save`.
4. Click `Install service`.
5. Confirm the Windows elevation prompt.
6. Click `Start service`.
7. Check the state with `Service status`.

Elevation is required only for installing, uninstalling, starting, and stopping the service. Normal TorrWind use does not require administrator rights.

If you use the service, do not start the same TorrServer as a second process on the same port.

## Connecting To A Remote TorrServer

If TorrServer is already running on another device:

1. Open `Settings -> Servers`.
2. Click `Add server`.
3. Enter a name, for example `Home server`.
4. Enter the URL:

   ```text
   http://192.168.1.2:8090
   ```

5. If authentication is enabled on the server, enter the username and password.
6. If the server uses a self-signed HTTPS certificate, you can enable `Ignore certificate errors`.
7. Click `Save`.
8. Click `Test connection`.

A remote server can be used for the library, search, Web UI, and playback just like a local server.

## Basic TorrServer Settings

Open `Settings -> TorrServer`.

Minimal starting values:

- `Cache`: keep `Memory`.
- `Cache size`: `64` is enough to start.
- `Preload %`: keep `50`.
- `Read ahead %`: keep `95`.
- `Disconnect timeout`: keep `30`.
- `Connections`: keep `25`.

If memory is limited or you want to keep cache on disk, switch cache to `Disk` and select a cache folder. For portable mode, a folder under `Data\TorrServer\cache` is convenient.

After changing runtime settings, click:

1. `Save`, to save TorrWind settings.
2. `Apply runtime settings`, to send them to the current TorrServer.

## TMDB And Posters

TMDB is used for poster and metadata lookup.

1. Get a TMDB API key from your TMDB account.
2. Open `Settings -> TorrServer`.
3. In the `TMDB` block, fill in:

   ```text
   API key
   API URL
   Image URL
   Image URL RU
   ```

4. Usually the default URLs can stay unchanged:

   ```text
   https://api.themoviedb.org
   https://image.tmdb.org
   https://imagetmdb.com
   ```

5. Click `Save`.
6. Click `Apply runtime settings`.

The API key is stored in TorrWind settings. When a support bundle is created, the key should not be included in the report as plain text.

## Adding A Torrent

Open `Library`.

You can add:

- a `.torrent` file through `Add .torrent`;
- a magnet link through `Add magnet`;
- a search result from the `Search` tab.

After adding a torrent, TorrWind refreshes its information. If the data does not appear immediately, click `Refresh` or `Refresh selected`.

## Watching Movies And Shows

1. Open `Library`.
2. Select a torrent on the left.
3. Select a file or episode below it.
4. Click `Open player`.

The built-in mpv player is used by default.

For shows, you can:

- select an episode from the playlist;
- go to the previous or next episode;
- use `Continue` to resume from the last position;
- use `Playlist from selected` to create a playlist from the selected episode to the last one.

The player supports audio, video, and subtitle settings:

- audio track selection;
- video track selection;
- subtitle selection;
- audio delay;
- subtitle delay;
- aspect ratio.

## External Player

If you want to use VLC, MPC-HC, PotPlayer, or another player:

1. Open `Settings -> Service`.
2. Find the player settings.
3. Choose the preferred player type.
4. For a custom player, set the path to its `.exe`.
5. Click `Save`.

If an external player does not open the link, check whether it supports TorrServer HTTP stream or M3U links.

## Search

TorrWind can search in two ways:

- through the selected TorrServer, if search is configured in TorrServer itself;
- directly through Torznab-compatible indexers such as Jackett, Prowlarr, or JacPro.

### Search Through TorrServer

1. Open `Search`.
2. Select `Selected TorrServer`.
3. Enter a query.
4. Click `Search`.

If search works in TorrServer Web UI but not in TorrWind, check the selected server, username/password, and diagnostics.

### Adding An Indexer

1. Open `Settings -> Indexers`.
2. Click `Add provider`.
3. Enter the indexer name.
4. Enter the Torznab/JacPro/Prowlarr/Jackett URL.
5. If an API key is required, enter it.
6. Fill categories if needed.
7. Click `Save`.

Example URLs:

```text
http://127.0.0.1:9117/api/v2.0/indexers/all/results/torznab
http://192.168.1.2:9696/api/v1/indexer/all/results/torznab
http://192.168.1.2:5002
```

After that, open `Search` and choose provider search.

## Web UI

The `Web UI` tab opens the embedded TorrServer web interface. It is useful when a function is not yet available in the native TorrWind interface.

If Web UI shows a script error:

1. Check that the correct server is selected.
2. Open Web UI in a normal browser using the same address.
3. Check that TorrServer is up to date.
4. Look at `Diagnostics -> Log`.

## Tray And Closing The App

TorrWind works as a tray application near the clock.

- The window close button can minimize the app to tray.
- The `Exit` button fully closes the GUI.
- If local TorrServer is running as a service, it can continue working after the GUI closes.
- If local TorrServer is running as a child process of the GUI, its lifetime depends on the launch mode and app settings.

## Settings Backups

Settings are stored in the working folder:

```text
Data\settings.json
```

The settings UI includes:

- settings export;
- settings import;
- backup restore;
- backup retention settings.

Before experimenting with servers, indexers, or Runtime JSON, it is useful to export settings.

## Diagnostics And Logs

If something does not work:

1. Open `Diagnostics -> Diagnostics`.
2. Click `Run diagnostics`.
3. Click `Save report` or `Support bundle`.
4. Open `Diagnostics -> Log` and check the latest errors.

Main logs:

```text
Data\logs\gui.jsonl
Data\logs\service.jsonl
Data\logs\mpv-player.log
```

The support bundle redacts sensitive values from settings, but it is still a good idea to review the report before sharing it.

## Common Problems

### TorrServer Does Not Start

- Check that the TorrServer file has been downloaded.
- Check that port `8090` is not already used by another process.
- If service mode is enabled, do not start a second local process on the same port.
- Open `Diagnostics -> Log`.

### Remote Server Does Not Work

- Check the URL in a browser.
- Check username and password.
- For HTTPS with a self-signed certificate, enable `Ignore certificate errors`.
- Check the firewall on the device where TorrServer is running.

### Search Does Not Work

- Check whether search works in TorrServer Web UI.
- For external indexers, check the URL and API key.
- Increase the indexer timeout.
- Check that the correct search mode is selected.

### Player Does Not Open Video

- Check that the torrent has received file information.
- Try `Refresh selected`.
- Try opening the same URL in external VLC/mpv.
- Check `Data\logs\mpv-player.log`.

### Audio Tracks Or Subtitles Are Missing

- Wait a few seconds after playback starts.
- Open track settings in the player.
- Check whether the file itself contains those tracks.

### Service Does Not Install Or Start

- Install the service through the TorrWind button and confirm administrator rights.
- Check the path to `TorrServer.exe`.
- Check `Data\logs\service.jsonl`.
- If you changed settings, click `Save` before starting the service.

## Safe Habits

- Keep the TorrWind portable folder in a location where the user has write permissions.
- Do not keep TorrWind under `Program Files` if you use portable mode without administrator rights.
- Do not publish your API keys, passwords, or magnet links as plain text.
- Make a settings backup before updates or manual JSON editing.
- For the first run, use the simplest setup: local TorrServer, 64 MB memory cache, built-in mpv.
