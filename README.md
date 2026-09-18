# PublicTxt.net

PublicTxt.Net is an in-progress .NET implementation of the [PublicTxt](https://github.com/publictxt/publictext) idea: Git repositories as an interoperability layer and free online storage for public and community knowledge kept as plain-text Markdown.

Planned shape:

- A **core library** for reading, writing, and syncing Markdown-based PublicTxt instances to Git repositories.
- **Feature services** for each content type (wiki, blog, bookmarks, community).
- **Infrastructure** projects: Git repository access and management, local database options.
- **Cross-platform clients**: a CLI, an Avalonia desktop app, and a Blazor web app.

See [docs/ProjectSpec.md](docs/ProjectSpec.md) for the design and [plan.md](plan.md) for what remains.

## Status

Early but usable from the command line: you can create an instance, write Markdown, sync it with a Git remote, inspect content, tags and links, and publish to a Pages branch. Subscriptions (the aggregation feature) are next.

| Project | Path | Status |
| --- | --- | --- |
| PublicTxt.Core | `src/Core/PublicTxt.Core` | Models, Markdown parsing (Markdig), instance layout, settings, content catalog with link resolution |
| PublicTxt.Git | `src/Infrastructure/PublicTxt.Git` | Implemented over LibGit2Sharp: sync cycle, credentials, tracking status, read-any-ref |
| PublicTxt.CLI | `src/Apps/PublicTxt.CLI` | `publictxt` tool: init, clone, status, list, show, links, commit, sync, publish |
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

## Quick start

```bash
alias publictxt='dotnet run --project src/Apps/PublicTxt.CLI --'

publictxt init ~/my-notes --remote https://github.com/you/my-notes.git --author "You <you@example.com>"
cd ~/my-notes
echo '# Hello' > wiki/hello.md
publictxt sync -m "first page"       # commit, pull, push  (token via --token or $PUBLICTXT_GIT_TOKEN)
publictxt list                       # what's in the instance
publictxt show wiki/hello.md         # metadata, tags, links, backlinks
publictxt links                      # broken internal links (exit 1 if any)
publictxt publish                    # push to gh-pages for static hosting
```

See [CLAUDE.md](CLAUDE.md) for the full command list.
