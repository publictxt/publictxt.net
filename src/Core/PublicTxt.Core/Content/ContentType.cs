namespace PublicTxt.Core.Content;

/// <summary>The content areas of a PublicTxt instance, each rooted at a path from <c>TxtInstanceSettings</c>.</summary>
public enum ContentType
{
    Unknown,
    Blog,
    Wiki,
    Notes,
    Bookmarks,
    Community,
    Media,
    Tags,
    Indexes,
    Settings
}

public static class ContentTypeExtensions
{
    /// <summary>Content types whose files are Markdown documents that get parsed.</summary>
    public static bool IsMarkdownContent(this ContentType type) => type is
        ContentType.Blog or ContentType.Wiki or ContentType.Notes or
        ContentType.Bookmarks or ContentType.Community or
        ContentType.Tags or ContentType.Indexes;
}
