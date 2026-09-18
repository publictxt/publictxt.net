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

Tests use xunit v3 and create throwaway git repositories under the system temp directory. Use `GitTestHelpers.DeleteDirectory` for cleanup; plain `Directory.Delete` fails on Windows because git object files are read-only. Core and CLI tests read the committed fixture at `tests/fixtures/sample-instance`; keep it small and never make it a git repository.

## CLI

The CLI assembly is `publictxt`. Run it from source with `dotnet run --project src/Apps/PublicTxt.CLI -- <command>`.

```text
publictxt init [path] [--remote <url>] [--no-commit]   create skeleton + git repo (+ initial commit)
publictxt clone <url> [path]                           clone an existing instance
publictxt status [--fetch]                             git + content summary
publictxt list [--type wiki|blog|notes|…] [--tag x]    table of items
publictxt show <file> [--body]                         one item: metadata, links, backlinks
publictxt links [--all]                                broken links (exit 1 if any) or every link
publictxt commit -m "…"                                stage all + commit
publictxt sync [-m "…"]                                commit (if -m), pull, push; exit 2 on conflicts
publictxt publish [--branch gh-pages]                  push current branch to a Pages branch

publictxt subscriptions add <url> [--name n] [--branch b]
    [--type wiki|blog|…] [--include glob] [--exclude glob]
    [--tag t] [--exclude-tag t]                        subscribe and fetch (alias: subs)
publictxt subscriptions list|update [name]|remove <name>
publictxt list --all [--search text] [--timeline]     local + subscribed content, with a source column
```

Subscription caches live under `<instance>/.publictxt/subscriptions/<name>` (git-ignored); the subscription list is `settings/subscriptions.json` and is meant to be committed.

All commands accept `--path`/`-C <dir>` (default: current directory). Author comes from `--author "Name <email>"`, then `PUBLICTXT_AUTHOR_NAME`/`PUBLICTXT_AUTHOR_EMAIL`, then git config. HTTPS token from `--token` or `PUBLICTXT_GIT_TOKEN`. SSH remotes are not supported (LibGit2Sharp lacks libssh2).

## Conventions

- Layering (see ProjectSpec §2): `Core` depends on nothing; `Features.*` and `Infrastructure.*` depend on `Core` only; `Apps.*` compose them.
- `PublicTxt.Git` speaks plain Git via LibGit2Sharp only. No GitHub/GitLab API calls.
- Work on feature branches (`feature/...`, `chore/...`) and merge into `main`.
