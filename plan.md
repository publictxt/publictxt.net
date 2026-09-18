# PublicTxt.net — Remaining Work

> Snapshot as of 2026-09-18. Grounded in the code on `main` and the milestones in [docs/ProjectSpec.md](docs/ProjectSpec.md). Update the checkboxes as work lands; move items between sections freely.

## 0. Where things stand

Verified on this machine (`dotnet 10.0.400`, Windows): the solution builds and all 25 tests pass.

| Area | State |
| --- | --- |
| `PublicTxt.Core` | Models plus `Content` (Markdig/YamlDotNet parser: title, front matter, tags, links, blog dates), `Instances` (layout conventions, JSON settings store, content reader, catalog with link resolution and backlinks) and `Subscriptions` (model, filter, store, aggregate catalog). 128 tests over a committed fixture instance. |
| `PublicTxt.Git` | `IGitRepository` / `IPrimaryRepository` / `IExternalRepository` over LibGit2Sharp 0.32: init, clone, stage, commit, fetch, pull, push with upstream tracking, branches, remotes, ahead/behind tracking status, credentials (HTTPS token), a `Sync` cycle, read-only access to any ref without checkout. `TxtInstanceGitService` drives all of it from a `TxtInstance`; `SubscriptionService` manages per-subscription caches. |
| `tests/GitTests` | 122 xunit v3 tests, including push/fetch/pull/merge/conflict and subscription scenarios against local bare repositories. |
| `PublicTxt.CLI` | `publictxt` on System.CommandLine: init, clone, status, list (with `--all`), show, links, commit, sync, publish, subscriptions. 39 in-process tests. |
| Feature projects (Wiki, Blog, Community, Bookmarks) | Do not exist yet. |
| `PublicTxt.Data`, Blazor, Avalonia | Do not exist yet. |
| CI | GitHub Actions build + test on ubuntu and windows (added 2026-09-18, not yet seen running). Dependabot for nuget, actions, devcontainers. |

Milestone status: **M1 done** (2026-09-18), **M2 done** (2026-09-19), **M3 done** (2026-09-19). M4–M6 not started. The v1 success statement from the big-pic doc is achievable from the CLI today.

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

- [x] `ExternalRepository.UpdateToBranch` (M3) follows any branch and resets the cache; `CloneOrUpdate` remains for the simple fast-forward case.
- [ ] `Pull`/`Sync` merge with default options; consider `MergeOptions` for `.gitattributes`-driven strategies later.
- [ ] Credentials are resolved per call; a caching/prompting resolver belongs with the CLI (M2) and desktop (M6).

---

## 3. M2 — Core content read + CLI (done 2026-09-19)

Goal from spec: parse and enumerate Markdown content per `TxtInstanceSettings` paths; CLI commands to inspect an instance. Landed on branches `feature/core-content-model`, `feature/core-instance-layout`, `feature/cli`.

### 3.1 `PublicTxt.Core`

- [x] **Content model**: `ContentItem` (path, type, title, front matter, tags, links, body, date, modified), `ContentLink` (internal / external / anchor, fragment, image), `FrontMatter` with typed accessors.
- [x] **Markdown parsing** via Markdig + YamlDotNet in `MarkdownContentParser`. Title: front matter → first H1 → file name. Malformed front matter is treated as absent.
- [x] **Instance enumeration**: `InstanceContentReader` walks each content directory, skips dot-entries, guards against root escapes. `BlogPathConvention` parses/builds `blog/yyyy/MM/dd/*.md` and `yyyyMMdd.md`.
- [x] **Link resolution**: `ContentCatalog` resolves relative, root-relative, percent-encoded and extensionless links, reports broken ones, answers backlinks.
- [x] **Tags decision**: both conventions, merged case-insensitively. Front matter `tags` (list or comma/space string) **and** Obsidian-style inline `#tag` in literal text (not headings, code, URLs; must contain a letter).
- [x] **Instance detection/validation**: `InstanceLayout.IsInstance` / `Validate` / `CreateSkeleton` (`.gitkeep` per content dir, never overwrites).
- [x] **Settings persistence decision**: JSON at `settings/instance.json` (camelCase, comments tolerated, unknown keys ignored) via `TxtInstanceSettingsStore`.
- [x] `tests/Core/CoreTests` with the committed fixture `tests/fixtures/sample-instance`.

### 3.2 `PublicTxt.CLI`

- [x] References Core + Git; **System.CommandLine 2.0** chosen (no colour/table dependency, easy in-process testing).
- [x] Commands: `init`, `clone`, `status [--fetch]`, `list [--type] [--tag]`, `show <file> [--body]`, `links [--all]`, `commit -m`, `sync [-m]`, `publish [--branch]`. Exit codes 0 / 1 / 2 (conflicts); `links` exits 1 when broken links exist.
- [x] Credentials: `--token` or `PUBLICTXT_GIT_TOKEN`. Author: `--author`, `PUBLICTXT_AUTHOR_NAME/EMAIL`, then git config. No SSH (see §2.2).
- [x] Documented in `CLAUDE.md` (full list) and README (quick start).
- [x] `tests/Apps/CliTests`: in-process tests over the fixture and a bare remote.

### 3.3 Follow-ups surfaced while doing M2 (not blocking M3)

- [ ] `publish` pushes the source branch as-is; static-site generation (index pages, HTML) is a later concern. GitHub Pages can serve raw Markdown via Jekyll, which is enough for now.
- [ ] `status`/`list`/`show` rebuild the whole catalog on every call; fine for small instances, M5 persistence addresses it.
- [ ] No `new` command yet (e.g. `publictxt new post` honouring the blog date layout); belongs with the Blog feature service in M4.
- [ ] `list` has no `--json` output; add when a client or script needs it.

---

## 4. M3 — External subscriptions (done 2026-09-19)

Goal from spec: subscribe to a remote PublicTxt repo with simple filters. Landed on branches `feature/subscriptions-core`, `feature/subscriptions-git`.

- [x] **Subscription model** (`Core.Subscriptions.Subscription`): name, remote URL, optional branch, filter, added/updated timestamps, last commit SHA.
- [x] **`SubscriptionFilter`**: content types, include/exclude path globs, include/exclude tags. Criteria ANDed, values ORed. `MatchesPath` pre-check avoids parsing files that cannot match.
- [x] **Local representation decision**: separate read-only clone per subscription at `<instance>/.publictxt/subscriptions/<name>`, git-ignored (the service adds the ignore entry). Aggregate is computed, never committed. `ExternalRepository.UpdateToBranch` hard-resets the cache, so force pushes and branch switches are harmless.
- [x] **Aggregate view** (`AggregateCatalog`): local + subscribed items with `Source`, per-source/type/tag queries, timeline, search; a failing subscription is reported, not fatal.
- [x] **Persistence** at `settings/subscriptions.json` (camelCase, lowercase enums), meant to be committed.
- [x] CLI: `subscriptions add|remove|list|update` (alias `subs`), `list --all [--search] [--timeline]`, subscription summary in `status`.
- [x] Tests: two published instances with different filters and a topic branch, at service level and through the CLI.

### 4.1 Follow-ups surfaced while doing M3 (not blocking M4)

- [ ] `subscriptions update` is sequential; parallel fetches would help with many subscriptions.
- [ ] Subscribed items are not link-resolved (no backlinks across sources). Cross-source link resolution needs a decision on how a link in Alice's page to another of Alice's pages should render locally.
- [ ] Caches are per instance. A per-user shared cache would avoid re-cloning the same remote for several instances.
- [ ] No `subscriptions edit`; changing a filter means remove + add (the cache is re-cloned). Could rewrite the JSON in place instead.
- [ ] `show` does not accept `<source>:<path>`; subscribed items can only be listed, not shown.
- [ ] The **v1 success statement** is now testable end to end from the CLI. Real-world use will surface the next priorities better than the plan can.

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
