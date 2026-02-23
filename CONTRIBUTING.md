# Contributing to LoginShot.DotNet

Thanks for contributing.

## Development setup

- Install .NET 8 SDK.
- Clone the repository.
- Build and test from the repo root:

```bash
dotnet restore LoginShot.sln
dotnet build LoginShot.sln
dotnet test LoginShot.sln
```

## Branching

- Do not work directly on `master`.
- Create a focused branch:
  - `feature/<short-description>`
  - `fix/<short-description>`
  - `test/<short-description>`
  - `docs/<short-description>`
  - `task/<short-description>`

## Pull requests

Keep PRs small and reviewable.

Each PR should include:

- Why the change is needed.
- What changed (high-level bullets).
- Test evidence (commands and outcomes).
- Risks or rollout notes when relevant.

## Tests and project layout

- `tests/LoginShot.Core.Tests`: core layer tests (config, storage, triggers, startup).
- `tests/LoginShot.Tests`: app layer tests (Windows-specific UI and orchestration).

## Coding and formatting

- Follow `.editorconfig`.
- Keep changes scoped; avoid unrelated formatting churn unless explicitly requested.
- Prefer clear, maintainable code over cleverness.

## Windows-specific behavior

This project targets Windows behavior (tray app, session events, Task Scheduler, camera capture).

- Validate Windows-specific changes on Windows when possible.
- Keep privacy and transparency constraints in mind.
