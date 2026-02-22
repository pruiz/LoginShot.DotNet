# AGENTS.md — LoginShot

This file guides coding agents (OpenCode, Cursor agents, Copilot agents, etc.) working on this repository.

## Goal (v1)

Build a Windows tray app that:
1) captures a webcam snapshot when the app starts after user login (`logon`),
2) captures a snapshot when the user session becomes active/unlocked (`unlock`),
3) attempts capture when the session is locked (`lock`, best-effort),
4) writes the image into a configurable folder,
5) writes a sidecar metadata JSON file per capture.

See `README.md` for user-facing behavior and examples.

Keep v1 local-only: no cloud APIs, no face recognition.

## Scope and Product Constraints

- Respect user privacy and OS constraints.
- Do not implement stealth behavior.
- Prefer explicit, inspectable behavior and logs.
- Default to local storage only.
- Avoid writing to cloud-synced directories by default unless explicitly configured.

## Tech Choices (v1)

- Language: C#
- Platform: Windows tray app
- Runtime: .NET 8 LTS
- Deployment target: Windows 10/11
- Camera: one-shot still capture via Windows/.NET-compatible camera APIs
- Event triggers:
  - On app start after login: capture once (`logon`)
  - On unlock/session active: capture once (`unlock`)
  - On lock/session inactive: capture once (`lock`, best-effort)
  - Debounce repeated OS signals
- Config: YAML
- Metadata sidecar: JSON (`System.Text.Json`)
- Logging: `Microsoft.Extensions.Logging`
- Concurrency: async/await (structured concurrency)
- Packaging: Windows app with tray icon and optional startup shortcut registration

## Repository Structure (target)

- `src/`
  - `LoginShot/`
    - `App/` (entrypoint, lifetime, tray icon/menu)
    - `Capture/` (camera access + one-shot capture)
    - `Triggers/` (logon/unlock/lock observers)
    - `Startup/` (startup shortcut register/unregister)
    - `Config/` (load/parse/defaults/path expansion)
    - `Storage/` (filenaming, atomic writes, sidecar JSON)
    - `Util/` (debounce, clock/time helpers, logging helpers)
- `tests/`
  - `LoginShot.Tests/`

Agents may scaffold this structure as needed.

## Functional Requirements

### One-shot capture
- Capture one still image quickly.
- Release camera resources after capture.
- Handle failures gracefully (log + continue, no crash).

### Triggers
- Capture once on app launch after login (`logon`).
- Capture once per unlock/session-active event (`unlock`).
- Attempt capture once per lock/session-inactive event (`lock`, best-effort).
- Debounce interval configurable (default `3` seconds).

### Storage
- Ensure output directory exists (create if missing).
- Filename format: `YYYY-MM-DDTHH-mm-ss-<event>.<ext>`
- Write files atomically (temp + rename/move).
- Write sidecar metadata JSON (`.json`) with same basename.

### Configuration
- Read from (first found wins):
  1. `%USERPROFILE%\.config\LoginShot\config.yml`
  2. `%APPDATA%\LoginShot\config.yml`
- Expand `%USERPROFILE%` and `%APPDATA%`.
- Provide safe defaults.
- Validate config and fail with clear diagnostics.

### UI
- Tray icon optional via config.
- If enabled, include:
  - `Capture now`
  - `Open output folder`
  - `Start after login` (startup shortcut toggle)
  - `Reload config`
  - `Generate sample config`
  - `Quit`

## Build / Lint / Test Commands (keep updated)

Choose commands based on project files present.

### .NET workflow (preferred)
- Build:
  - `dotnet build`
- Test all:
  - `dotnet test`
- Test a single test case:
  - `dotnet test --filter "FullyQualifiedName~<TestClass>.<TestMethod>"`
- Test a single test class:
  - `dotnet test --filter "FullyQualifiedName~<TestClass>"`
- Restore:
  - `dotnet restore`

### Lint/format (if configured)
- Format:
  - `dotnet format`
- Analyzer/lint:
  - `dotnet build -warnaserror` (when project enables analyzers)

If lint tools are not configured, follow existing style in touched files and avoid broad formatting churn.

## Coding Standards

### Imports
- Import only what is used.
- Keep imports at file top.
- Prefer deterministic ordering (alphabetical).
- Avoid adding heavy frameworks in shared utility files.

### Formatting
- Prefer repository formatter/linter config when present.
- Use consistent indentation and spacing.
- Keep functions focused and readable.
- Avoid unrelated reformatting in functional diffs.

### Types and API Design
- Prefer immutable models (`record`, `readonly struct`) where practical.
- Use interfaces for test seams around camera/events/filesystem.
- Keep access control explicit (`private`, `internal`, `public`).
- Use strong domain types for events/config values over raw strings.

### Naming
- Types: `UpperCamelCase`
- Methods/properties: `UpperCamelCase`
- Local variables/parameters: `lowerCamelCase`
- Enums/cases should be domain-descriptive (`logon`, `unlock`, `lock`)
- Tests should clearly state behavior and expectation.

### Error Handling
- Prefer typed exception types with actionable cases.
- Use `Exception` types and `Result`-like patterns for recoverable failures.
- Avoid null-forgiving (`!`) and unchecked null assumptions in production code.
- Avoid process-terminating paths on user/system failures; log and continue safely.
- Include context in logs (event, path, subsystem state).

### Concurrency / State
- Use structured concurrency where appropriate.
- Avoid data races around trigger handling and debounce.
- Keep mutable shared state minimal and explicit.
- Ensure capture pipeline can fail independently without taking down the app.

### File I/O and Metadata
- Use atomic writes for image + sidecar where possible.
- Keep sidecar schema stable and backwards-compatible.
- Ensure output folder and permissions are validated before capture write.

## Testing Expectations

- Add tests for all behavior changes.
- Prioritize unit tests for:
  - Config parsing + defaults
  - Path expansion
  - Filename formatting
  - Debounce logic
  - Startup shortcut registration/unregistration
  - Metadata sidecar generation
- Prefer deterministic tests with mocks/fakes for camera and clock.
- Add integration/dev harness only when needed (e.g., debug trigger).

## Rules Files (Cursor / Copilot)

None present at time of writing. If `.cursorrules`, `.cursor/rules/`, or `.github/copilot-instructions.md` are added, agents must read and follow them before making edits.

## Agent Workflow

- Read nearby code and mirror local patterns.
- Make the smallest correct change first.
- Run targeted/single tests before full suite.
- Update `README.md` for user-visible behavior/config changes.
- Do not revert unrelated working tree changes.
- Do not use destructive git operations unless explicitly requested.

## Repository Hosting and Collaboration Model

- Canonical remote for this repo: `https://github.com/pruiz/LoginShot.DotNet`.
- Treat this repository as PR-driven: all meaningful changes should flow through feature branches and pull requests.
- Default branch is `master` and should be treated as protected and long-lived.
- Agents should align with existing branch protection and review requirements when present.

## Branching Policy

- Never commit directly to `master`.
- Never push directly to `master`.
- Always work on a non-default branch for code, tests, docs, and refactors.
- Preferred branch names:
  - `feature/<short-description>`
  - `task/<short-description>`
  - `fix/<short-description>`
  - `docs/<short-description>`
  - `test/<short-description>`
- Keep branch scope focused; avoid mixing unrelated changes.
- Forbidden without explicit instruction: force push to `master`, destructive history edits (`reset --hard`, unsafe rebases) on shared branches.
- If currently on `master`, create/switch to a feature branch before making edits.

## Pull Request Workflow

- Open a PR for any change intended to merge.
- PRs should include:
  - Clear title (concise, imperative).
  - Why the change is needed.
  - What changed (high-level bullets).
  - Test evidence (commands run and outcomes).
  - Risks/rollout notes when applicable.
- Keep branch updated from `master` regularly to reduce drift/conflicts.
- Do not self-merge unless explicitly instructed and repository policy allows it.
- Prefer small PRs that are easy to review.

## Commit and Push Rules for Agents

- Do not create commits unless explicitly requested by the user/task.
- When committing, keep commits atomic and logically scoped.
- Use descriptive commit messages focused on intent.
- Never use force push (`--force` or `--force-with-lease`) unless explicitly instructed.
- Never rewrite published history unless explicitly instructed and safe.
- Do not amend commits after push unless explicitly requested.
- Never bypass hooks with `--no-verify` unless explicitly requested.
- Never commit secrets, credentials, tokens, or local environment files.

## Suggested Development Workflow for Agents

1. Sync and inspect branch status.
2. Create/use a non-default branch (`feature/...` or `task/...` as appropriate).
3. Make the smallest correct change.
4. Run targeted tests first, then broader tests as needed.
5. Run lint/format if configured.
6. Prepare concise commit(s) when requested.
7. Push branch and open/update PR with summary + test evidence.
8. Address review feedback with incremental commits.

## CI and Merge Expectations

- Ensure CI passes before merge (build/test/lint per repository policy).
- If CI fails, fix root cause before requesting merge.
- Prefer squash merge unless project policy specifies otherwise.
- After merge, clean up branches according to repository conventions.

## Notes / Pitfalls

- Camera permissions are controlled by Windows Privacy settings and can be denied per machine/user policy.
- Session lock/unlock signals may arrive in bursts; use debounce and event deduplication.
- `lock` capture is best-effort in v1 due to timing and camera availability constraints.
- Startup shortcuts can break if executable path changes; keep tray toggle idempotent and repair shortcuts when needed.
- Keep behavior transparent and auditable.

## Out of Scope (v1), possible future

- Full installer/MSIX/signing pipeline
- Cloud API uploads
- Face recognition or identity classification
- Retention/deletion policy automation
- Windows Service runtime mode with optional tray companion
- Multi-camera selection UX (tray submenu), camera verification action, and auto-persisted camera choice in config
