using PublicTxt.Core.Content;
using PublicTxt.Core.Instances;

namespace PublicTxt.Wiki;

/// <summary>A heading (letter or tag) and the wiki pages under it.</summary>
public sealed record WikiIndexSection(string Heading, IReadOnlyList<ContentItem> Pages);

/// <summary>Read-side wiki queries over a catalog: page lookup, home page, backlinks and indexes.</summary>
public sealed class WikiService
{
    private static readonly string[] HomeNames = ["index", "home", "readme"];

    private readonly ContentCatalog _catalog;

    /// <summary>Instance-relative wiki directory (normalised).</summary>
    public string WikiPath { get; }

    /// <summary>Wiki pages ordered by title, then path.</summary>
    public IReadOnlyList<ContentItem> Pages { get; }

    public WikiService(ContentCatalog catalog, string wikiPath)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        _catalog = catalog;
        WikiPath = InstanceLayout.Normalise(wikiPath);
        Pages = catalog.OfType(ContentType.Wiki)
            .OrderBy(p => p.Title, StringComparer.OrdinalIgnoreCase)
            .ThenBy(p => p.RelativePath, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>Builds the catalog from <paramref name="reader"/> and uses its configured wiki path.</summary>
    public static WikiService Open(InstanceContentReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);
        return new WikiService(reader.BuildCatalog(), reader.Settings.WikiPath);
    }

    /// <summary>The wiki root's <c>index</c>, <c>home</c> or <c>readme</c> page, in that order.</summary>
    public ContentItem? Home =>
        HomeNames.Select(n => _catalog.Get(InWiki(n + ".md"))).FirstOrDefault(p => p is not null && IsWikiPage(p));

    /// <summary>
    /// Finds a wiki page by, in order: path (instance- or wiki-relative, extension optional), file name,
    /// then title. Names and titles match case-insensitively; ties go to the first path. Empty → <see cref="Home"/>.
    /// </summary>
    public ContentItem? Find(string nameOrPath)
    {
        var key = InstanceLayout.Normalise(nameOrPath ?? string.Empty);
        if (key.Length == 0)
            return Home;

        foreach (var candidate in (string[])[key, key + ".md", InWiki(key), InWiki(key + ".md")])
        {
            if (_catalog.Get(candidate) is { } page && IsWikiPage(page))
                return page;
        }

        var byPath = Pages.OrderBy(p => p.RelativePath, StringComparer.Ordinal).ToList();
        if (!key.Contains('/') &&
            byPath.FirstOrDefault(p => FileStem(p).Equals(key, StringComparison.OrdinalIgnoreCase)) is { } byName)
            return byName;

        return byPath.FirstOrDefault(p => p.Title.Equals(key, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Items of any type that link to <paramref name="page"/>, ordered by title.</summary>
    public IReadOnlyList<ContentItem> Backlinks(ContentItem page)
    {
        ArgumentNullException.ThrowIfNull(page);
        return _catalog.Backlinks(page.RelativePath)
            .OrderBy(p => p.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>Pages grouped by the first letter of their title; non-letters go under <c>#</c>, which sorts first.</summary>
    public IReadOnlyList<WikiIndexSection> AlphabeticalIndex() =>
        Pages.GroupBy(p => IndexHeading(p.Title))
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => new WikiIndexSection(g.Key, g.ToList()))
            .ToList();

    /// <summary>One section per tag used by a wiki page, tags sorted case-insensitively. Untagged pages are omitted.</summary>
    public IReadOnlyList<WikiIndexSection> TagIndex() =>
        Pages.SelectMany(p => p.Tags)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(t => t, StringComparer.OrdinalIgnoreCase)
            .Select(tag => new WikiIndexSection(tag, Pages.Where(p => p.HasTag(tag)).ToList()))
            .ToList();

    // ── helpers ──────────────────────────────────────────────────────────────

    private bool IsWikiPage(ContentItem item) => item.Type == ContentType.Wiki;

    private string InWiki(string relative) => WikiPath.Length == 0 ? relative : WikiPath + "/" + relative;

    private static string FileStem(ContentItem item)
    {
        var name = item.FileName;
        var dot = name.LastIndexOf('.');
        return dot > 0 ? name[..dot] : name;
    }

    private static string IndexHeading(string title)
    {
        var first = title.TrimStart().FirstOrDefault();
        return char.IsLetter(first) ? char.ToUpperInvariant(first).ToString() : "#";
    }
}
