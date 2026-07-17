# TorrWind Completion Roadmap

## Implemented

- The MVP feature set in `PRODUCT_SPEC.md` is implemented.
- Settings, logs, backups, playlists, TorrServer data, and update packages stay under the application `Data` directory.
- TorrServer and TorrWind downloads verify the GitHub SHA256 digest or a matching checksum asset when available.
- Local TorrServer configuration files are replaced atomically.
- `TorrWindService` runs as `LocalService`; installation grants that account access to `Data` and grants interactive users only service query/start/stop rights.
- JSON locale loading is restricted to files found directly under `locales` and preserves the active translations if replacement locale files are invalid.
- Windows CI builds and tests every branch push and pull request. Release publication has a separate mandatory build/test gate.

## Remaining Acceptance Work

These checks require real Windows installations and cannot be completed reliably under Wine:

- Windows 10 x64 and Windows 11 x64 smoke tests for both portable and installer builds.
- Fresh service installation and migration of an existing `TorrWindService`; verify the `LocalService` account, start/stop without a UAC prompt, automatic startup, and access to `Data`.
- Installer upgrade/uninstall, startup task, `.torrent` association, and `magnet:` registration.
- Tray, WebView2 Web UI, dark/light/system themes, and DPI/scaling checks.
- Built-in mpv playback for MKV, AVI, MP4, M3U/M3U8, seeking, fullscreen, track selection, and multi-episode playlists.
- Local/remote TorrServer, HTTP authentication, ignored self-signed certificate errors, Torznab search, DLNA, and WebDAV smoke tests.

Record any failures as reproducible issues with the TorrWind version, Windows version, steps, and a support bundle.

## Conditional Phase 2: Release Signatures

The official [YouROK/TorrServer releases](https://github.com/YouROK/TorrServer/releases) currently publish SHA256 asset digests but do not provide a stable detached-signature format and a pinned public key. SHA256 protects download integrity but does not independently authenticate the publisher.

Signature verification will be implemented when the upstream release contract provides both trust materials:

1. A documented signature asset or signed manifest format.
2. An official public key and a stable fingerprint suitable for pinning in TorrWind.
3. A key-rotation/revocation procedure.

Once available, TorrWind will pin the trusted key, verify the signed manifest before accepting its asset digest, reject missing or invalid signatures for signed releases, and add positive, tampered-file, wrong-key, and rotated-key tests.
