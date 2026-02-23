# LoginShot

LoginShot is a Windows background tray app that captures a webcam snapshot when your user session reaches key states and stores the image plus metadata in a configurable local folder.

This aims to be the Windows counterpart to https://github.com/pruiz/LoginShot. (Which was in fact a PoC of an original idea by @aramosf)

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

## Release (unsigned)

LoginShot publishes unsigned Windows release artifacts via GitHub Actions.

1. Create a strict semver tag:
   ```bash
   git tag v1.2.3
   git push origin v1.2.3
   ```
2. GitHub Actions builds self-contained single-file artifacts and uploads four assets to the GitHub Release:
   - `LoginShot-windows-v1.2.3-win-x64-singlefile.zip`
   - `LoginShot-windows-v1.2.3-win-x64-singlefile.zip.sha256`
   - `LoginShot-windows-v1.2.3-win-arm64-singlefile.zip`
   - `LoginShot-windows-v1.2.3-win-arm64-singlefile.zip.sha256`

Verify checksum on Windows (PowerShell):

```powershell
Get-FileHash .\LoginShot-windows-v1.2.3-win-x64-singlefile.zip -Algorithm SHA256
```

Verify checksum on macOS/Linux:

```bash
shasum -a 256 -c LoginShot-windows-v1.2.3-win-x64-singlefile.zip.sha256
```

Important:
- Only strict semver tags are accepted (`vMAJOR.MINOR.PATCH`).
- Artifacts are unsigned and may trigger Windows SmartScreen warnings.
- Single-file artifacts extract native dependencies (OpenCV runtime) at startup via .NET bundle extraction.

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

LoginShot can self-register for user startup from the tray menu using Windows Task Scheduler:

- `Start after login` enabled: app creates/updates task `LoginShot.StartAfterLogin` with an `ONLOGON` trigger.
- `Start after login` disabled: app removes that scheduled task.
- Task action launches LoginShot with `--startup-trigger=logon`.

Startup registration is user-scoped and generally does not require admin rights. Some machine policies can still block Task Scheduler registration.

## Configuration (YAML)

LoginShot reads configuration from (first found wins):

1. `%USERPROFILE%\.config\LoginShot\config.yml`
2. `%APPDATA%\LoginShot\config.yml`

If no config file is found, LoginShot writes a default file to `%APPDATA%\LoginShot\config.yml` and uses safe defaults:
- **Output directory:** `%USERPROFILE%\Pictures\LoginShot`
- **Format:** `jpg` (1280px max width, 0.85 quality)
- **Triggers:** `logon`, `unlock`, and `lock` enabled
- **Metadata:** JSON sidecar enabled
- **Tray icon:** enabled
- **Debounce:** 3 seconds
- **Logging level:** `Information`

Path values in YAML may use either `\` or `/` separators. LoginShot normalizes configured Windows paths during load.

If a config file is found but invalid, LoginShot fails startup with clear diagnostics.

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
  backend: "opencv"   # "winrt-mediacapture" is accepted but currently falls back to OpenCV
  cameraIndex: null    # null = auto/default camera; otherwise 0, 1, 2...

logging:
  directory: "%LOCALAPPDATA%\\LoginShot\\logs"
  retentionDays: 14
  cleanupIntervalHours: 24
  level: "Information" # Trace|Debug|Information|Warning|Error|Critical|None

watermark:
  enabled: true
  format: "yyyy-MM-dd HH:mm:ss zzz"
```

Logs are written daily as `loginshot-YYYY-MM-DD.log` in `logging.directory`. LoginShot cleans up old log files at startup and periodically at `cleanupIntervalHours`, keeping files newer than `retentionDays`.
When enabled, watermark text is rendered at the bottom-right of captured images and includes hostname plus timestamp formatted using `watermark.format`.

## Output Files

Images are saved with timestamp and event tag:

- `2026-02-22T08-41-10-logon.jpg`
- `2026-02-22T09-00-05-unlock.jpg`
- `2026-02-22T12-15-30-lock.jpg`
- `2026-02-22T14-30-00-manual.jpg`

If enabled, sidecar metadata JSON is also written with the same basename:

- `2026-02-22T12-15-30-lock.json`

If camera capture fails, LoginShot still writes a failure sidecar with the same basename schema (no image file), and logs the failure.

Current capture backend is OpenCV. A WinRT `MediaCapture` backend is planned as a configurable alternative.

### Sidecar JSON schema (draft)
```json
{
  "timestamp": "2026-02-22T12:15:30.123Z",
  "event": "lock",
  "status": "success",
  "hostname": "WORKSTATION-01",
  "username": "pablo",
  "outputPath": "C:\\Users\\pablo\\Pictures\\LoginShot\\2026-02-22T12-15-30-lock.jpg",
  "failure": null,
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
| **Open log** | Open the current daily log file in your associated app |
| **Camera** | Select camera index (`Auto`, `Camera 0`, `Camera 1`, ...) and verify selection |
| **Start after login** | Toggle startup task registration in Task Scheduler |
| **Edit config** | Open the active `config.yml` in your default editor |
| **Reload config** | Re-read YAML config without full restart |
| **Quit** | Exit LoginShot |

When config file changes are detected, LoginShot attempts automatic reload. Successful reloads and reload errors are shown via tray balloon notifications. If a changed config is invalid, LoginShot keeps the previous valid in-memory configuration.

## Trigger Reliability Notes

- `logon` and `unlock` captures are expected behavior when events are delivered and camera is available.
- `logon` startup trigger is wired from scheduler launch (`--startup-trigger=logon`).
- session lock/unlock signals are routed through per-event-type debounce (`unlock` and `lock` are debounced independently).
- capture dispatch attempts real camera capture using OpenCV and persists success/failure sidecars.
- camera selection is currently index-based (`capture.cameraIndex`); friendly camera names are a planned future improvement.
- `lock` capture is **best-effort** in v1. Lock transitions can be timing-sensitive and camera access may fail depending on device/policy state.
- Failures should be logged with context and must not crash the app.

## Troubleshooting

- **No camera access / capture fails**
  - Windows Settings -> Privacy & security -> Camera.
  - Ensure camera access is enabled for desktop apps.
  - For deeper diagnostics, set `logging.level: "Debug"` in `config.yml`, then use tray menu `Open log` and inspect camera attempt lines.
- **Startup toggle does not work**
  - Verify task `LoginShot.StartAfterLogin` exists in Task Scheduler.
  - Confirm task action points to the current executable and includes `--startup-trigger=logon`.
- **Lock capture missing**
  - In v1 this is best-effort; inspect logs for event timing or camera acquisition failures.
- **Config not loading**
  - Check YAML syntax and verify one of the expected config paths exists.
- **Where are logs?**
  - Default path is `%LOCALAPPDATA%\LoginShot\logs`.
  - Verify `logging.directory` in config if you changed it.

## Roadmap

- v1: local snapshots (`logon`/`unlock`/`lock`), YAML config, JSON sidecar metadata, tray UI, startup toggle
- v1.x: camera selection improvements
  - improve friendly camera name support (currently index-based)
- future: optional Windows Service mode with companion tray UI
  - potential benefits: stronger startup reliability, reduced dependence on user startup folder, clearer long-running process management
  - tradeoffs to evaluate: session boundary complexity, camera access under service context, install/admin requirements
- future: evaluate migrating from custom file logger to a battle-tested logging sink (for example Serilog), still configured programmatically

## Security Notes

- v1 is local-only and does not require network access.
- Consider disk encryption and strict folder ACLs if captures are sensitive.

## License

[MIT](LICENSE)
