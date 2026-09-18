# PublicTxt.net

PublicTxt.Net is an in-progress .NET implementation of the [PublicTxt](https://github.com/publictxt/publictext) idea: Git repositories as an interoperability layer and free online storage for public and community knowledge kept as plain-text Markdown.

Planned shape:

- A **core library** for reading, writing, and syncing Markdown-based PublicTxt instances to Git repositories.
- **Feature services** for each content type (wiki, blog, bookmarks, community).
- **Infrastructure** projects: Git repository access and management, local database options.
- **Cross-platform clients**: a CLI, an Avalonia desktop app, and a Blazor web app.

See [docs/ProjectSpec.md](docs/ProjectSpec.md) for the design and [plan.md](plan.md) for what remains.

## Status

Early. The Git infrastructure layer exists and is tested; everything else is a stub or not yet created.

| Project | Path | Status |
| --- | --- | --- |
| PublicTxt.Core | `src/Core/PublicTxt.Core` | Models only (`TxtInstance`, `TxtInstanceSettings`) |
| PublicTxt.Git | `src/Infrastructure/PublicTxt.Git` | Implemented over LibGit2Sharp, with tests |
| PublicTxt.CLI | `src/Apps/PublicTxt.CLI` | Stub |
| PublicTxt.Wiki / Blog / Bookmarks / Community | `src/Features/*` | Planned |
| PublicTxt.Data | `src/Infrastructure/PublicTxt.Data` | Planned |
| PublicTxt.Avalonia / PublicTxt.Blazor | `src/Apps/*` | Planned |

## Build and test

Requires the .NET 10 SDK.

```bash
dotnet build PublicTxt.Net.sln
dotnet test PublicTxt.Net.sln
```

On Linux, a distrobox setup is provided under [.distrobox/](.distrobox/); see its README.
