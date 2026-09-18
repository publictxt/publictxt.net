using PublicTxt.Core.Content;

namespace PublicTxt.Core.Instances;

/// <summary>A link from one item, resolved against the instance.</summary>
/// <param name="Source">The item containing the link.</param>
/// <param name="Link">The link as parsed.</param>
/// <param name="ResolvedPath">Instance-relative path the link points at, or null when it escapes the root.</param>
/// <param name="Target">The catalogued Markdown item at that path, if any.</param>
/// <param name="Exists">True if a Markdown item or any other file exists at the resolved path.</param>
public sealed record ResolvedLink(
    ContentItem Source,
    ContentLink Link,
    string? ResolvedPath,
    ContentItem? Target,
    bool Exists)
{
    public bool IsBroken => !Exists;
}

/// <summary>An in-memory snapshot of an instance's Markdown items with tags and links resolved.</summary>
public sealed class ContentCatalog
{
    private readonly Dictionary<string, ContentItem> _byPath;
    private readonly ILookup<string, ResolvedLink> _incoming;

    public IReadOnlyList<ContentItem> Items { get; }

    /// <summary>Every internal link across all items, resolved.</summary>
    public IReadOnlyList<ResolvedLink> Links { get; }

    public IEnumerable<ResolvedLink> BrokenLinks => Links.Where(l => l.IsBroken);

    /// <summary>Distinct tags, case-insensitive, sorted.</summary>
    public IReadOnlyList<string> Tags { get; }

    private ContentCatalog(IReadOnlyList<ContentItem> items, IReadOnlyList<ResolvedLink> links)
    {
        Items = items;
        Links = links;
        _byPath = items.ToDictionary(i => i.RelativePath, StringComparer.OrdinalIgnoreCase);
        _incoming = links.Where(l => l.Target is not null).ToLookup(l => l.Target!.RelativePath, StringComparer.OrdinalIgnoreCase);
        Tags = items.SelectMany(i => i.Tags)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(t => t, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public ContentItem? Get(string relativePath) =>
        _byPath.TryGetValue(InstanceLayout.Normalise(relativePath), out var item) ? item : null;

    public IEnumerable<ContentItem> OfType(ContentType type) => Items.Where(i => i.Type == type);

    public IEnumerable<ContentItem> WithTag(string tag) => Items.Where(i => i.HasTag(tag));

    /// <summary>Items that link to the given path.</summary>
    public IEnumerable<ContentItem> Backlinks(string relativePath) =>
        _incoming[InstanceLayout.Normalise(relativePath)].Select(l => l.Source).Distinct();

    /// <summary>Resolved outgoing internal links of one item.</summary>
    public IEnumerable<ResolvedLink> LinksFrom(string relativePath)
    {
        var path = InstanceLayout.Normalise(relativePath);
        return Links.Where(l => l.Source.RelativePath.Equals(path, StringComparison.OrdinalIgnoreCase));
    }

    internal static ContentCatalog Build(InstanceContentReader reader)
    {
        var items = reader.EnumerateAll().ToList();
        var byPath = items.ToDictionary(i => i.RelativePath, StringComparer.OrdinalIgnoreCase);
        var links = new List<ResolvedLink>();

        foreach (var item in items)
        {
            foreach (var link in item.InternalLinks)
            {
                var resolved = ResolveTarget(item.Directory, link.Target);
                if (resolved is null)
                {
                    links.Add(new ResolvedLink(item, link, null, null, false));
                    continue;
                }

                // A target without an extension refers to a Markdown page.
                var candidates = HasExtension(resolved) ? new[] { resolved } : new[] { resolved + ".md", resolved };
                ResolvedLink? found = null;
                foreach (var candidate in candidates)
                {
                    if (byPath.TryGetValue(candidate, out var target))
                    {
                        found = new ResolvedLink(item, link, candidate, target, true);
                        break;
                    }
                    if (SafeFileExists(reader, candidate))
                    {
                        found = new ResolvedLink(item, link, candidate, null, true);
                        break;
                    }
                }

                links.Add(found ?? new ResolvedLink(item, link, candidates[0], null, false));
            }
        }

        return new ContentCatalog(items, links);
    }

    /// <summary>
    /// Resolves a link target relative to the source item's directory into a normalised instance-relative
    /// path. Root-relative targets (<c>/wiki/x.md</c>) resolve from the instance root. Returns null when the
    /// target climbs above the root.
    /// </summary>
    internal static string? ResolveTarget(string sourceDirectory, string target)
    {
        var decoded = Uri.UnescapeDataString(target).Replace('\\', '/');
        var isRootRelative = decoded.StartsWith('/');
        var combined = isRootRelative ? decoded.TrimStart('/') : (sourceDirectory.Length == 0 ? decoded : sourceDirectory + "/" + decoded);

        var stack = new List<string>();
        foreach (var segment in combined.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (segment == ".") continue;
            if (segment == "..")
            {
                if (stack.Count == 0) return null;
                stack.RemoveAt(stack.Count - 1);
                continue;
            }
            stack.Add(segment);
        }

        return stack.Count == 0 ? null : string.Join('/', stack);
    }

    private static bool HasExtension(string path)
    {
        var name = path[(path.LastIndexOf('/') + 1)..];
        return name.LastIndexOf('.') > 0;
    }

    private static bool SafeFileExists(InstanceContentReader reader, string relativePath)
    {
        try { return reader.FileExists(relativePath); }
        catch (UnauthorizedAccessException) { return false; }
    }
}
