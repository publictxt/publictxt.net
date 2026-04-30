# PublicTxt

PublicTxt is an experiment in using Git repositories as an interoperability
layer for public and community knowledge.

The idea: keep your notes, wiki, and writing as plain Markdown files in a Git
repository. Publish them to the web for free. Subscribe to other people's
PublicTxt repositories and pull selected content into a local aggregate view
you can browse and search — all without a centralised platform, server, or
subscription.

> Early stage. Tooling is in progress. The format is stable enough to use.

## Why Git?

Git hosting is free, ubiquitous, versioned, and nobody owns it. A PublicTxt
repository is just a folder of Markdown files — it works in any text editor,
any Git client, and any static site host today, regardless of whether
purpose-built PublicTxt tooling exists yet. The content outlives the tooling.

## The Core Idea

**Selective aggregation** is the differentiator. Anyone can put Markdown in
Git. What PublicTxt adds is a shared structure and convention that lets
software:

- Subscribe to another person's repository and pull only the parts you want
  (by topic branch, tag, path, or filter expression).
- Publish your own content to the web with a single push to a Pages-enabled
  branch.
- Sync bidirectionally with one or more of your own remotes.

Plain-text Markdown in Git is the *substrate*. Aggregation across many
repositories is the *value*.

## Format

Content is stored as standard Markdown using **md-wiki link syntax**
(`[Page Title](page.md)`), so any static site generator can publish it
without a conversion step.

PublicTxt syntax is compatible with [Obsidian](https://obsidian.md) — either
by configuring Obsidian to use Markdown links directly, or via format
conversion tooling.

Tags and structured metadata use plaintext conventions compatible with
Obsidian DataView attributes.

## Repository Structure

A PublicTxt repository follows this directory layout:

```
blog/
  2024/01/15/
    20240115.md
wiki/
notes/
media/
tags/
indexes/
settings/
```

Later-phase content types (not required for v1 use):

```
metaweb/        # bookmarks, annotations, web-page notes
community/      # discussion, profiles, groups
```

Full directory spec: [Directory Structure](#directory-structure) below.

## Using This Repository

This repository is both the home of the PublicTxt specification and a working
example of the format itself. You can:

- Fork it as a template for your own PublicTxt instance.
- Use it with any tooling built against the format.
- Suggest changes via pull requests, issues, or comments.

## Tooling

- **[PublicTxt.net](https://github.com/publictxt/publictxt.net)** — In-progress
  .NET implementation: core libraries, Git infrastructure, feature services,
  Avalonia desktop and Blazor web apps.
- **[WikiTool](https://github.com/jaysen/WikiTool)** — In-progress .NET tool
  for managing multiple wikis, converting between wiki formats (including
  Obsidian ↔ md-wiki), and copying content between wikis by tag/search
  expressions.

More tooling to follow.

## Directory Structure

v1 content types in **bold**; later-phase types in *italics*.

- **`blog/`** — Blog posts, organized by `year/month/day/`
- **`wiki/`** — Wiki pages
- **`notes/`** — Notes
- **`media/`** — Media files
- **`tags/`** — Tag indexes for the repository
- **`indexes/`** — Content indexes
- **`settings/`** — Repository settings
- *`metaweb/`* — Bookmarks, annotations, and notes about web pages *(later)*
  - *`sites/`, `bookmarks/`, `notes/`, `annotations/`, `indexes/`, `tags/`*
- *`community/`* — Discussion, profiles, groups *(later)*

## Future Ideas

These are part of the long-term vision, explicitly deferred until the core
aggregation-and-publish story is working:

- **Community & Social** — Discussion threads, profiles, groups, moderation,
  access controls, friendly merge UX for non-technical contributors.
- **MetaWeb / Web Annotation** — Browser extension for Git-backed page
  annotations; W3C Annotation-compatible social bookmarking.
- **Federation** — ActivityPub integration; immutable public records via Git
  commits.
- **Richer Data Models** — RDF semantic edges, weighted links, graph-database
  backend (e.g. TerminusDB), consensus and reputation mechanisms.

## License

[GPLv3](LICENSE)

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md).
