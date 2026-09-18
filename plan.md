# PublicTxt.net — Remaining Work

> Snapshot as of 2026-09-18. Grounded in the code on `main` and the milestones in [docs/ProjectSpec.md](docs/ProjectSpec.md). Update the checkboxes as work lands; move items between sections freely.

## 0. Where things stand

Verified on this machine (`dotnet 10.0.400`, Windows): the solution builds and all 25 tests pass.

| Area | State |
|---|---|
| `PublicTxt.Core` | Two model classes only: `TxtInstance`, `TxtInstanceSettings` (with path validation). No parsing, no content services, no tests. |
| `PublicTxt.Git` | `IGitRepository` / `IPrimaryRepository` / `IExternalRepository` implemented over LibGit2Sharp 0.32. Init, clone, stage, commit, fetch, pull (merge), push, checkout, read-only file access with path-traversal guard. |
| `tests/GitTests` | 25 xunit v3 tests. Cover local-only paths; no test exercises clone/fetch/push/pull against a real remote. |
| `PublicTxt.CLI` | `Console.WriteLine("Hello, World!")`. No project references. |
| Feature projects (Wiki, Blog, Community, Bookmarks) | Do not exist yet. |
| `PublicTxt.Data`, Blazor, Avalonia | Do not exist yet. |
| CI | Dependabot for devcontainers only. No build/test workflow. |

Milestone status: **M1 partially done** (git primitives exist, not wired to `TxtInstance`). M2–M6 not started.

---

## 1. Housekeeping (small, do first)

- [ ] **Finish the MetaWeb → Bookmarks rename in docs.** Still stale: [README.md:22](README.md#L22), [ProjectSpec.md:136](docs/ProjectSpec.md#L136), [ProjectSpec.md:166](docs/ProjectSpec.md#L166). (The "MetaWeb Commons" references in the big-pic docs are the future browser-extension concept and can stay.)
- [ ] **Make README honest about placeholders.** It lists Features, Data, Blazor, Avalonia, Tools as if they exist. Mark them "(planned)" or link to this plan.
- [ ] **Fill in `docs/CLAUDE.md`** build/test/run commands (currently "TBD"). Note that it lives under `docs/` so it is not auto-loaded; either move it to the repo root or add a root `CLAUDE.md` that points at it. Also note that the distrobox prefix only applies on Linux; on Windows plain `dotnet` works.
- [ ] **Add a GitHub Actions workflow** that runs `dotnet build` + `dotnet test` on push/PR (ubuntu + windows matrix, since LibGit2Sharp has native bits).
- [ ] **Extend Dependabot** to the `nuget` ecosystem.
- [ ] **Fix `.distrobox/setup.sh`**: it installs `dotnet-sdk-9.0` but every project targets `net10.0`.
- [ ] **Decide on tracked IDE files.** `.idea/**` and `PublicTxt.Net.sln.DotSettings.user` are committed. Either keep deliberately or `git rm --cached` and add to `.gitignore`.
- [ ] **Prune merged/stale remote branches**: `feature/git-infra-basic`, `feature/change-metaweb-references-to-bookmarks`, `codex/align-target-framework-...`, `copilot/add-simple-git-interfaces`.
- [ ] Optional: `Directory.Build.props` to centralise `net10.0` / `Nullable` / `ImplicitUsings`, and `Directory.Packages.props` for central package versions. Add solution folders (Core / Infrastructure / Apps / Tests) to the `.sln`.

---

## 2. M1 — Git foundation (finish)

Goal from spec: origin sync with a single remote, **wired into the `TxtInstance` lifecycle**.

### 2.1 Wire Git into `TxtInstance`
- [ ] Add a service in `PublicTxt.Git` (e.g. `TxtInstanceSyncService`) that takes a `TxtInstance` and drives `IPrimaryRepository` for it: open/clone from `RemoteUrl` into `LocalPath`, and update `InstanceStatus`, `GitStatus`, `CurrentBranch`, `LastGitSync`.
- [ ] Map repository state to the `GitStatus` enum (`Synced` / `LocalChanges` / `RemoteChanges` / `Diverged`). This needs **ahead/behind counts** against the tracking branch, which `WorkingTreeStatus` does not expose yet — extend it or add a `TrackingStatus` record.
- [ ] Decide whether `TxtInstance` gains an `Initialize()`/`Open()` step that validates `Settings` and the directory layout.

### 2.2 Gaps in `PrimaryRepository`
- [ ] **Credentials.** `Fetch`, `Pull`, `Push` pass `null` options, so only unauthenticated/local remotes work. Introduce `IGitCredentials` (spec §4.4) and plumb a `CredentialsHandler` (SSH key, PAT/HTTPS basic) through fetch/pull/push/clone.
- [ ] **Remote management**: add / list / remove remotes; set upstream tracking after `Push` on a new branch.
- [ ] **Branch creation** (`CreateBranch(name, fromCommit?)`). The `Checkout` test currently drops to raw LibGit2Sharp to create a branch — a sign the API is missing.
- [ ] **Guard empty commits.** `Commit` with nothing staged throws LibGit2Sharp's `EmptyCommitException`; either surface a clear domain exception or return null.
- [ ] Consider a `Sync()` convenience (fetch → pull → push) that returns a combined result, since that is what the CLI and apps will actually call.
- [ ] Pull is merge-only. Rebase can wait, but record the decision.

### 2.3 Gaps in `ExternalRepository`
- [ ] **Glob bug**: `ListFiles("**/*.md")` compiles to `^.*/[^/\\]*\.md$`, which requires a slash and therefore misses root-level `.md` files. Replace the hand-rolled converter with `Microsoft.Extensions.FileSystemGlobbing`.
- [ ] `FastForward` hardcodes a `system@localhost` signature. Fine for FF-only, but pass a `GitIdentity` for consistency or document why not.
- [ ] `ReadFile` reads the working tree; add `ReadFile(path, branchOrRef)` that reads from a tree without checking out, so topic-branch content can be read without switching branches.

### 2.4 Tests
- [ ] Add remote-based tests using a **local bare repository** as `origin`: clone, fetch, push, pull fast-forward, pull with a real merge, pull with conflicts (assert `ConflictedFiles`).
- [ ] `ExternalRepository`: `CloneOrUpdate` second call actually fast-forwards new commits; `GetRemoteBranches` lists topic branches.
- [ ] Regression test for the root-level glob bug above.

---

## 3. M2 — Core content read + CLI

Goal from spec: parse and enumerate Markdown content per `TxtInstanceSettings` paths; CLI commands to inspect an instance.

### 3.1 `PublicTxt.Core`
- [ ] **Content model**: a `ContentItem` (or per-type records) with relative path, content type, title, front matter, tags, outgoing links, modified time.
- [ ] **Markdown parsing** with [Markdig](https://github.com/xoofx/markdig) (YAML front matter extension, link extraction). Default dialect is md-wiki (`[text](page.md)`); Obsidian `[[wiki-links]]` are out of scope on disk per spec §3.3.
- [ ] **Instance enumeration**: walk each configured content path and yield items; blog items follow `blog/yyyy/MM/dd/*.md` and should get a parsed date.
- [ ] **Link resolution**: resolve relative md links to other items; report broken links.
- [ ] **Tags**: extract from front matter and/or inline `#tag` convention (pick one, document it in the spec).
- [ ] **Instance detection/validation**: "does this directory look like a PublicTxt instance?" plus creation of the default folder skeleton for `init`.
- [ ] **Settings persistence**: read/write `TxtInstanceSettings` from the instance's `settings/` folder (JSON or YAML — decide).
- [ ] New test project `tests/Core/CoreTests` with fixture instances under `tests/fixtures/`.

### 3.2 `PublicTxt.CLI`
- [ ] Add project references to `PublicTxt.Core` and `PublicTxt.Git`; pick a command framework (`System.CommandLine` or `Spectre.Console.Cli`).
- [ ] Commands for M1/M2:
  - `init [path]` — create instance skeleton + git init
  - `clone <url> [path]` — clone an existing instance
  - `status` — instance + git status (ahead/behind, dirty)
  - `list [--type blog|wiki|notes]` — enumerate content
  - `show <path>` — dump parsed item (front matter, tags, links)
  - `commit -m` / `sync` — stage all, commit, fetch/pull/push
  - `publish` — push to a Pages-enabled branch (the "zero-cost hosting" story from the big-pic doc; start with plain push, static-site generation later)
- [ ] Credential input for the CLI (env var / `--token` / SSH agent) — ties to §2.2.
- [ ] Document the commands in `docs/CLAUDE.md` and README.

---

## 4. M3 — External subscriptions (the differentiator)

Goal from spec: subscribe to a remote PublicTxt repo with simple filters.

- [ ] **Subscription model** in Core: name, remote URL, branch, filter rules, last-updated, local cache path.
- [ ] **`ISubscriptionFilter`** (spec §4.4): path glob, content type, tag, branch; composable. Start with path + content type + branch; tag filtering depends on M2 parsing.
- [ ] **Local representation** — open question §8 in the spec. Recommended starting point: each subscription is a separate `ExternalRepository` clone in a cache directory *outside* the instance's git tree (e.g. `<instance>/.publictxt/subscriptions/<name>` and git-ignored, or a per-user cache dir). The aggregate view is computed, not copied into the instance. Revisit "import into `subscriptions/` and commit" later if users want it.
- [ ] **Aggregate view**: enumerate filtered items across the instance + all subscriptions, using the M2 content model.
- [ ] **Subscription persistence** in `settings/subscriptions.*` so subscriptions travel with the instance.
- [ ] CLI: `subscribe <url> [--branch] [--path] [--type]`, `unsubscribe`, `subscriptions list`, `subscriptions update [name]`, and `list --all` to include aggregated content.
- [ ] Tests: two local source repos → one instance with two subscriptions and differing filters.

---

## 5. M4 — Feature services

Create the four projects under `src/Features/` (each references Core only, per layering rules) and give each a thin service interface. Keep them small; most value is in Core + Git.

- [ ] `PublicTxt.Wiki` — page lookup by name/path, backlinks, index generation.
- [ ] `PublicTxt.Blog` — posts by date range, "new post for today" helper honouring the `yyyy/MM/dd` layout, feed/index generation.
- [ ] `PublicTxt.Bookmarks` — site/URL → filename mapping (`www.example.com-folder-page.md`), bookmark/annotation records.
- [ ] `PublicTxt.Community` — placeholder only; explicitly deferred by the big-pic doc.
- [ ] Decide where **Notes** lives (Core vs its own project) — spec open question.
- [ ] Update `PublicTxt.Net.sln` and README as projects appear.

---

## 6. M5 — Persistence (`PublicTxt.Data`)

- [ ] SQLite via `Microsoft.Data.Sqlite` (or EF Core if the apps want it) storing the M2 content index across the instance and subscriptions.
- [ ] FTS5 full-text search over titles/bodies/tags.
- [ ] Index is a **rebuildable cache** derived from disk + git; never the source of truth. `reindex` command.
- [ ] Incremental refresh keyed on git commit SHA / file mtime.

---

## 7. M6 — Clients

- [ ] **Avalonia desktop** first (the distrobox scripts already install Avalonia deps): instance picker, content browser/search, editor with md-wiki links, sync button, subscriptions manager.
- [ ] **Blazor web** later; needs the credential-storage decision (spec open question) resolved for a browser context.
- [ ] Shared view-model / application layer between the two so the CLI, desktop and web all call the same services.

---

## 8. Open decisions to close (from spec §8, with suggested defaults)

| Question | Suggested default to unblock work |
|---|---|
| Topic-branch naming | `topic/<name>`; subscribers filter on the prefix. Document in spec. |
| WikiTool integration | Reference it as a NuGet/project dependency only at the app/CLI edge for import/export; Core stays md-wiki only. |
| Notes content type owner | Core, alongside Media/Tags/Indexes. |
| Origin-sync conflict UX | CLI: report conflicted files and stop; desktop: later. Never auto-resolve. |
| Local shape of subscribed content | Separate read-only clones outside the instance tree; computed aggregate view (see §4). |
| Credential storage | CLI: env var / git credential helper / SSH agent. Desktop: OS keychain. Web: TBD. |
| Settings file format | JSON in `settings/instance.json` (matches .NET tooling; YAML is fine if Obsidian-friendliness matters more). |

---

## 9. Explicitly not planned for v1

Community/social features, browser extension, ActivityPub, ledgers, RDF/semantic syntax, TerminusDB, reputation systems. See "Future Ideas" in [docs/PublicTxt-big-pic.md](docs/PublicTxt-big-pic.md).

---

## 10. Suggested order of attack

1. Housekeeping (§1) — an afternoon.
2. Finish M1 (§2): credentials, ahead/behind, remote-based tests, `TxtInstance` wiring.
3. M2 Core parsing + a real CLI (§3). At this point the tool is usable for personal notes.
4. M3 subscriptions (§4). At this point the "v1 success" statement in the big-pic doc is testable.
5. M4/M5/M6 as needed by real usage.
