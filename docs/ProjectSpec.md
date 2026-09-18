# PublicTxt.net — Project Specification

> Status: **Early draft.** Many sections are intentionally light and will be filled in as the design firms up. See [PublicTxt-big-pic.md](PublicTxt-big-pic.md) for the broader project vision.

## 1. Purpose & Scope

PublicTxt.net is a .NET implementation of the [PublicTxt](https://github.com/publictxt/publictext) idea: using Git repositories as an interoperability layer and online storage for public and community knowledge stored as plain-text Markdown.

This document specifies the .NET solution: its libraries, services, infrastructure, and client applications. It does not re-derive the PublicTxt format itself (see the big-pic doc and the parent repo for that).

### 1.1 Goals

- Provide a reusable **core library** for reading, writing, and reasoning about a PublicTxt instance (a local working copy of a Git repository containing PublicTxt-formatted content).
- Provide **Git infrastructure** that lets a PublicTxt instance sync with one or more remotes, and selectively aggregate content from external repos.
- Provide **feature services** that handle the distinct content types (wiki, blog, community, bookmarks).
- Provide **client applications** (desktop, web, CLI) built on those services.
- Stay **provider-agnostic** — speak standard Git only; no GitHub/GitLab API dependencies in core.

### 1.2 Non-Goals (for now)

- A persistence layer beyond Git itself (planned: `PublicTxt.Data`, later).
- Provider-specific integrations (GitHub PRs, GitLab issues, etc.).
- ActivityPub, distributed ledger, graph DB, browser extension — see the big-pic doc's "Future Ideas".

## 2. Solution Layout

```bash
src/
  Core/
    PublicTxt.Core              — domain models, parsing, content services
  Features/
    PublicTxt.Wiki              — wiki content service        (placeholder)
    PublicTxt.Blog              — blog content service        (placeholder)
    PublicTxt.Community         — community content service   (placeholder)
    PublicTxt.Bookmarks         — bookmarks content service   (placeholder)
  Infrastructure/
    PublicTxt.Git               — Git abstractions over LibGit2Sharp
    PublicTxt.Data              — local persistence            (placeholder)
  Apps/
    PublicTxt.CLI               — command-line tool
    PublicTxt.Blazor            — web client                  (placeholder)
    PublicTxt.Avalonia          — desktop client              (placeholder)
tests/
docs/
```

Layering rules:

- `Core` depends on no other PublicTxt project.
- `Features.*` depend on `Core` only.
- `Infrastructure.*` depend on `Core` only.
- `Apps.*` may depend on `Core`, `Features.*`, and `Infrastructure.*`.
- No project under `Features` or `Infrastructure` depends on another sibling — composition happens in `Apps` (or via a thin composition root in `Core`, TBD).

## 3. Core Concepts

### 3.1 TxtInstance

A `TxtInstance` is a single PublicTxt working copy: a local directory that is also a Git working tree, containing Markdown content laid out per the PublicTxt directory structure. See [TxtInstance.cs](../src/Core/PublicTxt.Core/Models/TxtInstance.cs).

An instance has:

- **Identity**: id, name, local path.
- **Git state**: current branch, last sync time, sync status, one or more remotes (see §4).
- **Lifecycle status**: `Uninitialized`, `Ready`, `Syncing`, `Error`.
- **Settings** (`TxtInstanceSettings`): per-content-type relative paths, sync preferences, default branch.

### 3.2 Content Types

A PublicTxt instance contains several content types, each rooted at a settings-configured relative path:

| Content type | Default path | Owning feature project |
|---|---|---|
| Blog        | `blog`      | `PublicTxt.Blog`      |
| Wiki        | `wiki`      | `PublicTxt.Wiki`      |
| Notes       | `notes`     | `PublicTxt.Core` (TBD) |
| Bookmarks   | `bookmarks` | `PublicTxt.Bookmarks` |
| Community   | `community` | `PublicTxt.Community` |
| Media       | `media`     | `PublicTxt.Core`      |
| Tags        | `tags`      | `PublicTxt.Core`      |
| Indexes     | `indexes`   | `PublicTxt.Core`      |
| Settings    | `settings`  | `PublicTxt.Core`      |

Feature services are intentionally sparse for now — they exist to scope content-type-specific logic but most details are TBD.

### 3.3 PublicTxt Format

PublicTxt content is plain Markdown. The **default on-disk format is md-wiki** (standard Markdown links: `[text](page.md)`), chosen so that an instance can be published directly via GitHub Pages and similar static-site hosts without a conversion step.

**Obsidian compatibility** is still a goal but is handled at the editing edge, not on disk. Two paths to support:

1. **Conversion via [WikiTool](https://github.com/jaysen/WikiTool)** — convert between Obsidian wiki-link syntax (`[[page]]`) and md-wiki (`[page](page.md)`) on import/export.
2. **Obsidian in Markdown-link mode** — Obsidian can be configured to use standard Markdown links, in which case no conversion is needed.

`PublicTxt.Core` parses and emits md-wiki by default. Format details live in the parent repo; conversion to/from other wiki dialects is delegated to WikiTool (or an integration with it — TBD).

## 4. Git Infrastructure

`PublicTxt.Git` provides .NET abstractions over Git operations, implemented on top of **LibGit2Sharp**. There are two distinct abstractions:

### 4.1 Origin Sync (the instance's own remotes)

The instance's content is synced bidirectionally with one or more **origin remotes**. These are the canonical homes for the instance's content.

- Initially: a single origin remote (the typical case).
- Later: multiple origin remotes — the instance can publish to several places at once.
- Operations: clone, fetch, pull, push, commit, branch, status.

### 4.2 External Repo Subscription

The instance can also **subscribe to external PublicTxt repositories** and pull selected content from them into local read-only views (or merged into local content per user rules).

- Subscriptions are read-only by default — the instance is a consumer, not a publisher, of external repo content.
- Pulls are **selective**: filtered by topic branch, tag, content type, path, or arbitrary expression. Filtering rules are per-subscription.
- Topic-branch naming conventions are **TBD**.

### 4.3 Provider Agnosticism

`PublicTxt.Git` speaks only standard Git — clone, fetch, push, branches, refs, commits. No GitHub/GitLab/Gitea API calls. Any provider-specific features (PRs, issues, forks-as-API) belong in a future, optional adapter layer.

### 4.4 Interfaces

Implemented (see `src/Infrastructure/PublicTxt.Git`):

- `IGitRepository` — shared inspection: branch, latest commit, working-tree status (incl. conflicts), tracking status (upstream / ahead / behind), remotes, local branches, fetch.
- `IPrimaryRepository` — the instance's own working copy: init, clone, stage, commit, pull (merge), push (sets upstream), checkout, create branch, add/remove remote, and `Sync` (commit → fetch → merge → push, stopping before push on conflicts).
- `IExternalRepository` — a subscribed repo: clone-or-update, remote branch discovery, fast-forward, and reading files or listing trees from the working tree **or from any ref without checkout** (`ReadFileAt`, `ListFilesAt`).
- `IGitCredentials` — provider-agnostic credential resolution, threaded through every network call. Implementations: static token, environment variable (`PUBLICTXT_GIT_TOKEN`), delegate. **Only HTTPS username/token is supported**; the LibGit2Sharp native binaries are built without libssh2, so SSH would need a separate git-CLI adapter.
- `TxtInstanceGitService` — bridges `TxtInstance` and `IPrimaryRepository`: initialise (clone or init), refresh status, sync.

Subscriptions (M3):

- `Subscription` / `SubscriptionFilter` (Core) — name, remote URL, optional branch, and a filter of content types, include/exclude path globs and include/exclude tags. Criteria are ANDed, values within a criterion ORed.
- `SubscriptionStore` (Core) — persists the list at `settings/subscriptions.json`, committed with the instance.
- `AggregateCatalog` (Core) — local items plus filtered subscribed items, each carrying a `Source`; a failing subscription is reported, not fatal.
- `SubscriptionService` (Git) — one read-only clone per subscription under `<instance>/.publictxt/subscriptions/<name>` (git-ignored). Add clones before persisting; update hard-resets each cache to the followed branch so force pushes and branch switches are harmless.

Decisions recorded: pull/sync merge rather than rebase; `TxtInstance` stays a plain model and lifecycle lives in services.

## 5. Persistence (`PublicTxt.Data`) — Placeholder

Out of scope for the initial milestone. When introduced, it will provide a local cache/index outside Git for fast search and browsing. Likely candidates: SQLite first; later, optionally graph-based (e.g. TerminusDB, per the big-pic doc).

## 6. Feature Services — Placeholders

Each of `PublicTxt.Wiki`, `PublicTxt.Blog`, `PublicTxt.Community`, and `PublicTxt.Bookmarks` will own the read/write/query logic for its content type. Details deferred. They depend only on `PublicTxt.Core`.

## 7. Applications

### 7.1 PublicTxt.CLI

Command-line entry point for batch operations, scripting, and headless use. Assembly name `publictxt`, built on System.CommandLine.

| Command | Purpose |
| --- | --- |
| `init [path] [--remote url]` | Create the instance skeleton, `settings/instance.json`, git repo and initial commit |
| `clone <url> [path]` | Clone an existing instance |
| `status [--fetch]` | Git state (branch, upstream, ahead/behind, working tree) and content counts |
| `list [--type t] [--tag x]` | Table of content items |
| `show <file> [--body]` | One item: metadata, front matter, resolved links, backlinks |
| `links [--all]` | Broken internal links (exit 1 if any) or every internal link |
| `commit -m` | Stage all and commit |
| `sync [-m]` | Commit (if `-m`), fetch, merge, push; exit 2 on conflicts |
| `publish [--branch gh-pages]` | Push the current branch to a Pages-style branch |
| `subscriptions add <url> [--name] [--branch] [--type] [--include] [--exclude] [--tag] [--exclude-tag]` | Subscribe and fetch |
| `subscriptions list` / `update [name]` / `remove <name>` | Manage subscriptions |
| `list --all [--search] [--timeline]` | Aggregate view across local and subscribed content |

All commands take `--path`/`-C`. Author: `--author "Name <email>"`, then `PUBLICTXT_AUTHOR_NAME`/`_EMAIL`, then git config. Credentials: `--token` or `PUBLICTXT_GIT_TOKEN` (HTTPS only). See [CLAUDE.md](../CLAUDE.md) for details.

### 7.2 PublicTxt.Blazor — Placeholder

Web client for interactive editing and viewing. Out of scope for the initial milestone.

### 7.3 PublicTxt.Avalonia — Placeholder

Cross-platform desktop client. Out of scope for the initial milestone.

## 8. Open Questions

- Topic-branch naming convention.
- WikiTool integration shape — referenced library, CLI shell-out, or port the converter into Core?
- Notes content type — owned by Core, or its own feature project? (Parsing already lives in Core; a Notes feature project is only needed if notes gain behaviour beyond plain pages.)
- Resolved (M2): **tags** come from both front matter `tags` and inline `#tag`, merged case-insensitively. **Settings** are JSON at `settings/instance.json`.
- Conflict-resolution UX for origin sync. (Infrastructure now reports conflicts via `GitSyncResult.HasConflicts` / `GitStatus.Conflicted` and never auto-resolves; the UX is a client concern.)
- Resolved (M3): subscribed content lives in **separate read-only clones outside the instance's tracked tree** (`.publictxt/subscriptions/`, git-ignored) and the aggregate view is computed. Importing selected pages into the instance and committing them remains a possible later feature.
- Credential storage strategy across CLI / desktop / web. (`IGitCredentials` is the seam; CLI will start with the `PUBLICTXT_GIT_TOKEN` environment resolver.)
- SSH support: not possible through LibGit2Sharp's bundled binaries. Options if needed: a git-CLI adapter, or HTTPS tokens only.

## 9. Milestones (rough)

1. **M1 — Git foundation.** ✅ Done 2026-09-18. `PublicTxt.Git` with origin sync (single remote): clone, fetch, pull, push, commit, status, credentials, sync cycle. Wired into `TxtInstance` via `TxtInstanceGitService`.
2. **M2 — Core content read.** ✅ Done 2026-09-19. Markdig-based parsing, instance layout and JSON settings, content catalog with link resolution and backlinks. `publictxt` CLI with init/clone/status/list/show/links/commit/sync/publish.
3. **M3 — External subscriptions.** ✅ Done 2026-09-19. Subscriptions with type/path/tag filters and branch following, per-subscription caches, aggregate view, CLI commands.
4. **M4 — Feature services.** Flesh out Wiki / Blog / Bookmarks / Community APIs.
5. **M5 — Persistence.** Introduce `PublicTxt.Data` for local cache/search.
6. **M6 — Clients.** Avalonia and Blazor apps.
