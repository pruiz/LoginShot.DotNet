# LoginShot

LoginShot is a Windows background tray app that captures a webcam snapshot when your user session reaches key states and stores the image plus metadata in a configurable local folder.

This aims to be the windows counterpart to https://github.com/pruiz/LoginShot. (Which was infact a PoC of an original idea by @aramosf)

v1 trigger events:
- `logon` (app starts after user login)
- `unlock` (session becomes active/unlocked)
- `lock` (best-effort capture at lock transition)

> **Privacy notice**
> This tool records images from your camera. Use it only on devices you own/administer, and only in compliance with applicable laws and policies. In many places you must disclose camera-based monitoring.

## v1 Scope

### Included
- Windows tray app (.NET 8)
- One-shot snapshot capture from the system camera
- Event-based capture on `logon`, `unlock`, and `lock` (lock is best-effort)
- YAML configuration file
- Image storage plus JSON sidecar metadata
- Optional startup registration via tray menu (`Start after login`)

### Not included (for now)
- Cloud upload APIs (Dropbox/Google Drive/etc.)
- Face recognition / identity classification
- Retention/deletion automation
- Windows Service runtime mode (planned)

## Requirements
- Windows 10/11
- .NET 8 SDK (for development)
- Camera device and camera access enabled in Windows privacy settings

## Build and Test

From repository root:

```bash
dotnet build LoginShot.sln
dotnet test LoginShot.sln
```

If/when multiple projects are added, run build/test against the solution (`.sln`) or specific project paths.

## Installation (developer mode)

1. Clone:
   ```bash
   git clone https://github.com/pruiz/LoginShot.DotNet.git
   cd LoginShot.DotNet
   ```
2. Build and run the tray app once:
   ```bash
   dotnet run --project src/LoginShot/LoginShot.csproj
   ```
3. Grant camera permissions in Windows when requested (or via Settings).
4. Optionally create or generate a config file (see Configuration).
5. Enable startup from tray menu using `Start after login`.

## Startup Behavior

LoginShot can self-register for user startup from the tray menu:

- `Start after login` enabled: app creates/updates a shortcut in the current user's Startup folder.
- `Start after login` disabled: app removes the startup shortcut.

Startup folder path:
- `%APPDATA%\Microsoft\Windows\Start Menu\Programs\Startup`

This keeps startup behavior visible and user-controlled without requiring admin rights.

## Configuration (YAML)

LoginShot reads configuration from (first found wins):

1. `%USERPROFILE%\.config\LoginShot\config.yml`
2. `%APPDATA%\LoginShot\config.yml`

If no config file is found, LoginShot uses safe defaults:
- **Output directory:** `%USERPROFILE%\Pictures\LoginShot`
- **Format:** `jpg` (1280px max width, 0.85 quality)
- **Triggers:** `logon`, `unlock`, and `lock` enabled
- **Metadata:** JSON sidecar enabled
- **Tray icon:** enabled
- **Debounce:** 3 seconds

### YAML example
```yaml
output:
  directory: "%USERPROFILE%\\Pictures\\LoginShot"
  format: "jpg"
  maxWidth: 1280
  jpegQuality: 0.85

triggers:
  onLogon: true
  onUnlock: true
  onLock: true

metadata:
  writeSidecar: true

ui:
  trayIcon: true
  startAfterLogin: false

capture:
  debounceSeconds: 3
```

## Output Files

Images are saved with timestamp and event tag:

- `2026-02-22T08-41-10-logon.jpg`
- `2026-02-22T09-00-05-unlock.jpg`
- `2026-02-22T12-15-30-lock.jpg`
- `2026-02-22T14-30-00-manual.jpg`

If enabled, sidecar metadata JSON is also written with the same basename:

- `2026-02-22T12-15-30-lock.json`

### Sidecar JSON schema (draft)
```json
{
  "timestamp": "2026-02-22T12:15:30.123Z",
  "event": "lock",
  "hostname": "WORKSTATION-01",
  "username": "pablo",
  "outputPath": "C:\\Users\\pablo\\Pictures\\LoginShot\\2026-02-22T12-15-30-lock.jpg",
  "app": {
    "id": "LoginShot",
    "version": "0.1.0",
    "build": "1"
  },
  "camera": {
    "deviceName": "Integrated Camera"
  }
}
```

## Tray Menu

When tray icon is enabled, LoginShot exposes:

| Menu item | Description |
|-----------|-------------|
| **Capture now** | Take a snapshot immediately (`manual` event) |
| **Open output folder** | Open configured output directory in Explorer |
| **Start after login** | Toggle startup shortcut registration |
| **Reload config** | Re-read YAML config without full restart |
| **Generate sample config** | Write sample `config.yml` in `%APPDATA%\\LoginShot\\` (no overwrite) |
| **Quit** | Exit LoginShot |

## Trigger Reliability Notes

- `logon` and `unlock` captures are expected behavior when events are delivered and camera is available.
- `lock` capture is **best-effort** in v1. Lock transitions can be timing-sensitive and camera access may fail depending on device/policy state.
- Failures should be logged with context and must not crash the app.

## Troubleshooting

- **No camera access / capture fails**
  - Windows Settings -> Privacy & security -> Camera.
  - Ensure camera access is enabled for desktop apps.
- **Startup toggle does not work**
  - Verify shortcut exists in `%APPDATA%\Microsoft\Windows\Start Menu\Programs\Startup`.
  - Confirm target path still points to the current executable location.
- **Lock capture missing**
  - In v1 this is best-effort; inspect logs for event timing or camera acquisition failures.
- **Config not loading**
  - Check YAML syntax and verify one of the expected config paths exists.

## Roadmap

- v1: local snapshots (`logon`/`unlock`/`lock`), YAML config, JSON sidecar metadata, tray UI, startup toggle
- v1.x: camera selection improvements
  - enumerate all available cameras
  - tray submenu to select active camera
  - `Verify camera` action to test selected device
  - auto-update config when camera selection changes from tray
- future: optional Windows Service mode with companion tray UI
  - potential benefits: stronger startup reliability, reduced dependence on user startup folder, clearer long-running process management
  - tradeoffs to evaluate: session boundary complexity, camera access under service context, install/admin requirements

## Security Notes

- v1 is local-only and does not require network access.
- Consider disk encryption and strict folder ACLs if captures are sensitive.

## License

[MIT](LICENSE)
