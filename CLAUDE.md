# Claude Code Instructions

## Context to read first

- [docs/ProjectSpec.md](docs/ProjectSpec.md) — design, layering rules, milestones.
- [plan.md](plan.md) — what remains; tick items off as they land.
- [docs/PublicTxt-big-pic.md](docs/PublicTxt-big-pic.md) — the wider PublicTxt vision (background only).

## Environment

Requires the .NET 10 SDK. Run commands from the repository root.

- **Windows / macOS / any host with the SDK installed:** run `dotnet` directly.
- **Linux via distrobox:** the project can also run inside the `dotnetbox` container described in [.distrobox/README.md](.distrobox/README.md). Prefix commands with `distrobox enter dotnetbox --` when working from the host.

## Build, test, run

```bash
dotnet build PublicTxt.Net.sln
dotnet test PublicTxt.Net.sln
dotnet test tests/Infrastructure/GitTests/GitTests.csproj   # just the git tests
dotnet run --project src/Apps/PublicTxt.CLI                 # CLI (currently a stub)
```

Tests use xunit v3 and create throwaway git repositories under the system temp directory. Use `GitTestHelpers.DeleteDirectory` for cleanup; plain `Directory.Delete` fails on Windows because git object files are read-only.

## Conventions

- Layering (see ProjectSpec §2): `Core` depends on nothing; `Features.*` and `Infrastructure.*` depend on `Core` only; `Apps.*` compose them.
- `PublicTxt.Git` speaks plain Git via LibGit2Sharp only. No GitHub/GitLab API calls.
- Work on feature branches (`feature/...`, `chore/...`) and merge into `main`.
