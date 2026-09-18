namespace PublicTxt.Core.Content;

/// <summary>A parsed Markdown document inside a PublicTxt instance.</summary>
public sealed class ContentItem
{
    /// <summary>Path relative to the instance root, always with forward slashes (e.g. <c>wiki/page.md</c>).</summary>
    public required string RelativePath { get; init; }

    public required ContentType Type { get; init; }

    /// <summary>From front matter <c>title</c>, else the first level-1 heading, else the file name.</summary>
    public required string Title { get; init; }

    public FrontMatter FrontMatter { get; init; } = FrontMatter.Empty;

    /// <summary>Union of front-matter <c>tags</c> and inline <c>#tags</c>, de-duplicated case-insensitively, original casing kept.</summary>
    public IReadOnlyList<string> Tags { get; init; } = Array.Empty<string>();

    public IReadOnlyList<ContentLink> Links { get; init; } = Array.Empty<ContentLink>();

    /// <summary>Markdown body with the front matter block removed.</summary>
    public string Body { get; init; } = string.Empty;

    /// <summary>Front-matter <c>date</c>, or for blog posts the date encoded in the path/file name.</summary>
    public DateOnly? Date { get; init; }

    public DateTimeOffset? ModifiedAt { get; init; }

    /// <summary>
    /// Where the item came from: null for the local instance, otherwise the subscription name.
    /// Set by the aggregate view, not by the parser.
    /// </summary>
    public string? Source { get; init; }

    public bool IsLocal => Source is null;

    public string FileName => RelativePath[(RelativePath.LastIndexOf('/') + 1)..];

    /// <summary>Directory part of <see cref="RelativePath"/> (forward slashes, no trailing slash, empty at root).</summary>
    public string Directory
    {
        get
        {
            var i = RelativePath.LastIndexOf('/');
            return i < 0 ? string.Empty : RelativePath[..i];
        }
    }

    public IEnumerable<ContentLink> InternalLinks => Links.Where(l => l.Kind == ContentLinkKind.Internal);

    public bool HasTag(string tag) => Tags.Contains(tag, StringComparer.OrdinalIgnoreCase);

    /// <summary>Copy of this item attributed to <paramref name="source"/>.</summary>
    public ContentItem WithSource(string? source) => new()
    {
        RelativePath = RelativePath,
        Type = Type,
        Title = Title,
        FrontMatter = FrontMatter,
        Tags = Tags,
        Links = Links,
        Body = Body,
        Date = Date,
        ModifiedAt = ModifiedAt,
        Source = source
    };

    public override string ToString() => Source is null ? $"{Type}: {RelativePath}" : $"{Type}: {Source}:{RelativePath}";
}
