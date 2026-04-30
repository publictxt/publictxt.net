# PublicTxt.net — Project Specification

> Status: **Early draft.** Many sections are intentionally light and will be filled in as the design firms up. See [PublicTxt-big-pic.md](PublicTxt-big-pic.md) for the broader project vision.

## 1. Purpose & Scope

PublicTxt.net is a .NET implementation of the [PublicTxt](https://github.com/publictxt/publictext) idea: using Git repositories as an interoperability layer and online storage for public and community knowledge stored as plain-text Markdown.

This document specifies the .NET solution: its libraries, services, infrastructure, and client applications. It does not re-derive the PublicTxt format itself (see the big-pic doc and the parent repo for that).

### 1.1 Goals

- Provide a reusable **core library** for reading, writing, and reasoning about a PublicTxt instance (a local working copy of a Git repository containing PublicTxt-formatted content).
- Provide **Git infrastructure** that lets a PublicTxt instance sync with one or more remotes, and selectively aggregate content from external repos.
- Provide **feature services** that handle the distinct content types (wiki, blog, community, metaweb).
- Provide **client applications** (desktop, web, CLI) built on those services.
- Stay **provider-agnostic** — speak standard Git only; no GitHub/GitLab API dependencies in core.

### 1.2 Non-Goals (for now)

- A persistence layer beyond Git itself (planned: `PublicTxt.Data`, later).
- Provider-specific integrations (GitHub PRs, GitLab issues, etc.).
- ActivityPub, distributed ledger, graph DB, browser extension — see the big-pic doc's "Future Ideas".

## 2. Solution Layout

```
src/
  Core/
    PublicTxt.Core              — domain models, parsing, content services
  Features/
    PublicTxt.Wiki              — wiki content service        (placeholder)
    PublicTxt.Blog              — blog content service        (placeholder)
    PublicTxt.Community         — community content service   (placeholder)
    PublicTxt.MetaWeb           — metaweb content service     (placeholder)
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
| MetaWeb     | `metaweb`   | `PublicTxt.MetaWeb`   |
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

### 4.4 Interfaces (sketch — TBD)

The following abstractions are anticipated; exact shapes will emerge as the implementation lands:

- `IOriginRepository` — operations on the instance's own working copy and its origin remote(s).
- `IExternalRepository` — read-only operations against a subscribed external repo.
- `ISubscriptionFilter` — encapsulates the rules for which content from an external repo gets pulled.
- `IGitCredentials` — credential resolution (SSH key, PAT, etc.), provider-agnostic.

## 5. Persistence (`PublicTxt.Data`) — Placeholder

Out of scope for the initial milestone. When introduced, it will provide a local cache/index outside Git for fast search and browsing. Likely candidates: SQLite first; later, optionally graph-based (e.g. TerminusDB, per the big-pic doc).

## 6. Feature Services — Placeholders

Each of `PublicTxt.Wiki`, `PublicTxt.Blog`, `PublicTxt.Community`, and `PublicTxt.MetaWeb` will own the read/write/query logic for its content type. Details deferred. They depend only on `PublicTxt.Core`.

## 7. Applications

### 7.1 PublicTxt.CLI

Command-line entry point for batch operations, scripting, and headless use (publish, sync, convert, etc.). Currently a stub; commands TBD.

### 7.2 PublicTxt.Blazor — Placeholder

Web client for interactive editing and viewing. Out of scope for the initial milestone.

### 7.3 PublicTxt.Avalonia — Placeholder

Cross-platform desktop client. Out of scope for the initial milestone.

## 8. Open Questions

- Topic-branch naming convention.
- WikiTool integration shape — referenced library, CLI shell-out, or port the converter into Core?
- Notes content type — owned by Core, or its own feature project?
- Conflict-resolution UX for origin sync (multiple remotes diverging).
- How subscription-pulled external content is represented locally (separate worktree? imported into a `subscriptions/` tree? merged?).
- Credential storage strategy across CLI / desktop / web.

## 9. Milestones (rough)

1. **M1 — Git foundation.** `PublicTxt.Git` with origin sync (single remote): clone, fetch, pull, push, commit, status. Wired into `TxtInstance` lifecycle.
2. **M2 — Core content read.** Parse and enumerate Markdown content per `TxtInstanceSettings` paths. CLI commands to inspect an instance.
3. **M3 — External subscriptions.** Subscribe to a remote PublicTxt repo with simple filters.
4. **M4 — Feature services.** Flesh out Wiki / Blog / MetaWeb / Community APIs.
5. **M5 — Persistence.** Introduce `PublicTxt.Data` for local cache/search.
6. **M6 — Clients.** Avalonia and Blazor apps.
