using Microsoft.Extensions.FileSystemGlobbing;
using PublicTxt.Core.Content;

namespace PublicTxt.Core.Subscriptions;

/// <summary>
/// Which content from a subscribed repository to pull in. All populated criteria must match
/// (AND); within a criterion any value matches (OR). Empty criteria match everything.
/// </summary>
public sealed class SubscriptionFilter
{
    /// <summary>Content types to include, e.g. <c>[Blog, Wiki]</c>. Empty = all Markdown content types.</summary>
    public List<ContentType> Types { get; set; } = [];

    /// <summary>Include globs on the repo-relative path (e.g. <c>wiki/recipes/**</c>). Empty = all paths.</summary>
    public List<string> Paths { get; set; } = [];

    /// <summary>Exclude globs applied after <see cref="Paths"/>.</summary>
    public List<string> ExcludePaths { get; set; } = [];

    /// <summary>Tags of which an item must carry at least one. Empty = no tag requirement.</summary>
    public List<string> Tags { get; set; } = [];

    /// <summary>Tags that exclude an item when present.</summary>
    public List<string> ExcludeTags { get; set; } = [];

    public bool IsEmpty =>
        Types.Count == 0 && Paths.Count == 0 && ExcludePaths.Count == 0 && Tags.Count == 0 && ExcludeTags.Count == 0;

    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();
        foreach (var glob in Paths.Concat(ExcludePaths))
        {
            if (string.IsNullOrWhiteSpace(glob))
                errors.Add("path filter must not be empty");
            else if (glob.StartsWith('/') || glob.Contains(".."))
                errors.Add($"path filter must be repo-relative without '..': {glob}");
        }
        foreach (var tag in Tags.Concat(ExcludeTags))
        {
            if (string.IsNullOrWhiteSpace(tag))
                errors.Add("tag filter must not be empty");
        }
        return errors;
    }

    /// <summary>
    /// Cheap pre-check on path and type alone, so callers can skip parsing files that can never match.
    /// </summary>
    public bool MatchesPath(string relativePath, ContentType type)
    {
        if (Types.Count > 0 && !Types.Contains(type))
            return false;

        var path = relativePath.Replace('\\', '/').TrimStart('/');

        if (Paths.Count > 0 && !AnyGlobMatches(Paths, path))
            return false;
        if (ExcludePaths.Count > 0 && AnyGlobMatches(ExcludePaths, path))
            return false;

        return true;
    }

    /// <summary>Full check including tags.</summary>
    public bool Matches(ContentItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (!MatchesPath(item.RelativePath, item.Type))
            return false;

        if (Tags.Count > 0 && !Tags.Any(item.HasTag))
            return false;
        if (ExcludeTags.Count > 0 && ExcludeTags.Any(item.HasTag))
            return false;

        return true;
    }

    /// <summary>Human-readable summary such as <c>types=blog,wiki paths=wiki/** tags=recipes</c>, or <c>(everything)</c>.</summary>
    public string Describe()
    {
        var parts = new List<string>();
        if (Types.Count > 0) parts.Add("types=" + string.Join(",", Types.Select(t => t.ToString().ToLowerInvariant())));
        if (Paths.Count > 0) parts.Add("paths=" + string.Join(",", Paths));
        if (ExcludePaths.Count > 0) parts.Add("exclude=" + string.Join(",", ExcludePaths));
        if (Tags.Count > 0) parts.Add("tags=" + string.Join(",", Tags));
        if (ExcludeTags.Count > 0) parts.Add("-tags=" + string.Join(",", ExcludeTags));
        return parts.Count == 0 ? "(everything)" : string.Join(" ", parts);
    }

    private static bool AnyGlobMatches(IEnumerable<string> globs, string path)
    {
        var matcher = new Matcher(StringComparison.OrdinalIgnoreCase);
        foreach (var glob in globs)
        {
            // A bare directory name means everything below it.
            var g = glob.Replace('\\', '/').TrimStart('/');
            matcher.AddInclude(g.EndsWith('/') ? g + "**" : g);
            if (!g.Contains('*') && !g.Contains('?') && !g.Contains('.'))
                matcher.AddInclude(g + "/**");
        }
        return matcher.Match(path).HasMatches;
    }
}
