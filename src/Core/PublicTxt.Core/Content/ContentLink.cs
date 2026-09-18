namespace PublicTxt.Core.Content;

public enum ContentLinkKind
{
    /// <summary>A relative path inside the instance, e.g. <c>../wiki/page.md</c>.</summary>
    Internal,
    /// <summary>An absolute URL with a scheme, e.g. <c>https://…</c> or <c>mailto:…</c>.</summary>
    External,
    /// <summary>A same-document fragment, e.g. <c>#section</c>.</summary>
    Anchor
}

/// <summary>A link found in a Markdown document.</summary>
/// <param name="Target">The raw link destination as written, without any fragment.</param>
/// <param name="Text">The link text, or null for images with no alt text.</param>
/// <param name="Kind">How the target should be interpreted.</param>
/// <param name="Fragment">The <c>#fragment</c> part, if any (without the hash).</param>
/// <param name="IsImage">True for <c>![alt](src)</c> image references.</param>
public sealed record ContentLink(
    string Target,
    string? Text,
    ContentLinkKind Kind,
    string? Fragment = null,
    bool IsImage = false);
