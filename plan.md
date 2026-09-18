# PublicTxt.net — Remaining Work

> Snapshot as of 2026-09-18. Grounded in the code on `main` and the milestones in [docs/ProjectSpec.md](docs/ProjectSpec.md). Update the checkboxes as work lands; move items between sections freely.

## 0. Where things stand

Verified on this machine (`dotnet 10.0.400`, Windows): the solution builds and all 25 tests pass.

| Area | State |
| --- | --- |
| `PublicTxt.Core` | Two model classes only: `TxtInstance`, `TxtInstanceSettings` (with path validation). No parsing, no content services, no tests. |
| `PublicTxt.Git` | `IGitRepository` / `IPrimaryRepository` / `IExternalRepository` over LibGit2Sharp 0.32: init, clone, stage, commit, fetch, pull, push with upstream tracking, branches, remotes, ahead/behind tracking status, credentials (HTTPS token), a `Sync` cycle, read-only access to any ref without checkout. `TxtInstanceGitService` drives all of it from a `TxtInstance`. |
| `tests/GitTests` | 100 xunit v3 tests, including push/fetch/pull/merge/conflict scenarios against a local bare repository. |
| `PublicTxt.CLI` | `Console.WriteLine("Hello, World!")`. No project references. |
| Feature projects (Wiki, Blog, Community, Bookmarks) | Do not exist yet. |
| `PublicTxt.Data`, Blazor, Avalonia | Do not exist yet. |
| CI | GitHub Actions build + test on ubuntu and windows (added 2026-09-18, not yet seen running). Dependabot for nuget, actions, devcontainers. |

Milestone status: **M1 done** (2026-09-18). M2–M6 not started.

---

## 1. Housekeeping (small, do first)

Done 2026-09-18 on branches `chore/docs-housekeeping`, `chore/ci`, `chore/dev-env`, `chore/build-props`.

- [x] **Finish the MetaWeb → Bookmarks rename in docs.** (The "MetaWeb Commons" references in the big-pic docs are the future browser-extension concept and stay.)
- [x] **Make README honest about placeholders.** README now has a status table.
- [x] **`CLAUDE.md`** moved to the repo root with real build/test/run commands; distrobox prefix documented as Linux-only.
- [x] **GitHub Actions workflow** `.github/workflows/build.yml`: build + test on ubuntu and windows. Not yet observed running — verify after the first push.
- [x] **Dependabot** now covers `nuget`, `github-actions` and `devcontainers`.
- [x] **`.distrobox/setup.sh`** installs the .NET 10 SDK.
- [x] **IDE files untracked.** `.idea/` and `*.DotSettings.user` are git-ignored; files remain on disk. Reverse with `git add -f` if shared settings are wanted.
- [ ] **Prune merged/stale remote branches** (left for a human; deletes on the remote):

  ```bash
  git push origin --delete feature/git-infra-basic feature/change-metaweb-references-to-bookmarks \
    codex/align-target-framework-and-references-for-publictxt copilot/add-simple-git-interfaces
  git branch -d feature/change-metaweb-references-to-bookmarks chore/docs-housekeeping chore/ci chore/dev-env chore/build-props
  ```

- [x] `Directory.Build.props` (net10.0 / Nullable / ImplicitUsings), `Directory.Packages.props` (central package versions), and Core / Infrastructure / Apps / Tests solution folders.

---

## 2. M1 — Git foundation (done 2026-09-18)

Goal from spec: origin sync with a single remote, **wired into the `TxtInstance` lifecycle**. Landed on branches `feature/git-external-repo-reads`, `feature/git-primary-repo-api`, `feature/git-credentials`, `feature/instance-sync`.

### 2.1 Wire Git into `TxtInstance`

- [x] `TxtInstanceGitService` (in `PublicTxt.Git`): `Initialize` clones from `RemoteUrl` or inits locally and adds `origin`; `Refresh` maps repo state onto `TxtInstance`; `Sync` runs a cycle and records `LastGitSync`. Failures set `InstanceStatus.Error`.
- [x] `TrackingStatus` (upstream, ahead, behind) on `IGitRepository`; `ComputeGitStatus` maps tree + tracking to the `GitStatus` enum, which gained `Conflicted`.
- [x] **Decision:** `TxtInstance` stays a plain model. Lifecycle lives in services (`TxtInstanceGitService` now; a Core-side instance/layout validator in M2).

### 2.2 `PrimaryRepository`

- [x] **Credentials.** `IGitCredentials` with `Static`, `Environment` (`PUBLICTXT_GIT_TOKEN`) and `Delegate` resolvers, threaded through fetch/pull/push/clone. HTTPS token only: the LibGit2Sharp native binaries lack libssh2, so **SSH is not supported through this backend**. Revisit with a git-CLI shell-out adapter if SSH becomes necessary.
- [x] Remotes: `AddRemote` / `RemoveRemote` / `GetRemotes`; `Push` sets upstream tracking and refuses an unborn branch.
- [x] `CreateBranch(name, checkout)` and `GetLocalBranches`.
- [x] Empty commit → `InvalidOperationException("Nothing to commit…")`.
- [x] `Sync(identity, commitMessage?, remote)` → `GitSyncResult`; stops before push on conflicts.
- [x] **Decision:** pull is merge-only for now. Rebase is deferred until a client needs linear history.

### 2.3 `ExternalRepository`

- [x] Glob via `Microsoft.Extensions.FileSystemGlobbing`; `**/*.md` now includes root-level files.
- [x] `FastForward` signature documented as a placeholder (FF-only never creates a commit).
- [x] `ReadFileAt(ref, path)` and `ListFilesAt(ref, glob)` read any branch/tag/SHA without checkout.

### 2.4 Tests

- [x] Bare-repo remote tests: push/tracking, clone, fetch-only, ahead/behind/diverged, pull up-to-date / fast-forward / merge / conflicts, A-B-A round trip, topic-branch push then read via `ExternalRepository`.
- [x] `CloneOrUpdate` fast-forwards; `GetRemoteBranches` lists topic branches; glob regression.

### 2.5 Follow-ups surfaced while doing M1 (not blocking M2)

- [ ] `ExternalRepository.CloneOrUpdate` fast-forwards only the checked-out branch; subscriptions that follow several topic branches will need per-branch refs (M3).
- [ ] `Pull`/`Sync` merge with default options; consider `MergeOptions` for `.gitattributes`-driven strategies later.
- [ ] Credentials are resolved per call; a caching/prompting resolver belongs with the CLI (M2) and desktop (M6).

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
- [ ] Document the commands in `CLAUDE.md` and README.

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
| --- | --- |
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
