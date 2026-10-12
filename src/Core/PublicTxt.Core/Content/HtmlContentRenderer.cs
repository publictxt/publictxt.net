using Markdig;
using Markdig.Extensions.AutoIdentifiers;
using Markdig.Renderers.Html;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using PublicTxt.Core.Instances;

namespace PublicTxt.Core.Content;

/// <summary>Maps instance-relative paths to the URLs a client serves them at.</summary>
/// <param name="Page">URL of a catalogued Markdown page. Must return a URL the client trusts.</param>
/// <param name="File">URL of any other file, e.g. media.</param>
public sealed record ContentUrls(Func<string, string> Page, Func<string, string> File)
{
    /// <summary>
    /// Root-relative, percent-encoded per segment; pages lose their Markdown extension
    /// (<c>wiki/My Page.md</c> → <c>/wiki/My%20Page</c>, <c>media/logo.png</c> → <c>/media/logo.png</c>).
    /// </summary>
    public static ContentUrls Default { get; } = new(p => "/" + Escape(StripMarkdownExtension(p)), p => "/" + Escape(p));

    private static string Escape(string path) => string.Join('/', path.Split('/').Select(Uri.EscapeDataString));

    private static string StripMarkdownExtension(string path)
    {
        foreach (var ext in (string[])[".md", ".markdown"])
            if (path.EndsWith(ext, StringComparison.OrdinalIgnoreCase))
                return path[..^ext.Length];
        return path;
    }
}

/// <summary>
/// Renders an item's Markdown body to HTML for display. Content is treated as untrusted: raw HTML is
/// escaped, only <see cref="AllowedSchemes"/> survive as external links, and every internal link is
/// rewritten through <see cref="ContentUrls"/> (broken ones get the <c>broken-link</c> class).
/// </summary>
public sealed class HtmlContentRenderer
{
    public static IReadOnlySet<string> AllowedSchemes { get; } =
        new HashSet<string>(["http", "https", "mailto"], StringComparer.OrdinalIgnoreCase);

    // Extensions are chosen one by one: UseAdvancedExtensions includes generic attributes
    // ({onclick=...}), which would reopen the script injection DisableHtml closes.
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UsePipeTables()
        .UseEmphasisExtras()
        .UseTaskLists()
        .UseFootnotes()
        .UseAutoLinks()
        .UseAutoIdentifiers(AutoIdentifierOptions.GitHub)
        .DisableHtml()
        .Build();

    private readonly ContentCatalog _catalog;
    private readonly ContentUrls _urls;

    public HtmlContentRenderer(ContentCatalog catalog, ContentUrls? urls = null)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        _catalog = catalog;
        _urls = urls ?? ContentUrls.Default;
    }

    public string Render(ContentItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        var document = Markdown.Parse(item.Body, Pipeline);

        foreach (var link in document.Descendants<LinkInline>().ToList())
            Rewrite(item, link);

        // <scheme:...> autolinks bypass LinkInline; unsafe ones become plain text.
        foreach (var autolink in document.Descendants<AutolinkInline>().ToList())
        {
            if (!autolink.IsEmail && !IsAllowedExternal(autolink.Url))
                autolink.ReplaceBy(new LiteralInline(autolink.Url));
        }

        return document.ToHtml(Pipeline);
    }

    private void Rewrite(ContentItem item, LinkInline link)
    {
        if (string.IsNullOrWhiteSpace(link.Url))
            return;

        var parsed = MarkdownContentParser.Classify(link.Url, isImage: link.IsImage);
        switch (parsed.Kind)
        {
            case ContentLinkKind.Anchor:
                return;

            case ContentLinkKind.External when IsAllowedExternal(parsed.Target):
                if (!link.IsImage)
                {
                    link.GetAttributes().AddClass("external-link");
                    link.GetAttributes().AddProperty("rel", "noopener noreferrer");
                }
                return;

            case ContentLinkKind.External:
                link.Url = "#";
                link.GetAttributes().AddClass("unsafe-link");
                return;

            default:
                var resolved = _catalog.Resolve(item, parsed);
                var fragment = parsed.Fragment is null ? string.Empty : "#" + parsed.Fragment;
                if (resolved.Target is not null)
                    link.Url = _urls.Page(resolved.ResolvedPath!) + fragment;
                else if (resolved.Exists)
                    link.Url = _urls.File(resolved.ResolvedPath!) + fragment;
                else
                {
                    link.Url = resolved.ResolvedPath is null ? "#" : _urls.Page(resolved.ResolvedPath);
                    link.GetAttributes().AddClass("broken-link");
                }
                return;
        }
    }

    private static bool IsAllowedExternal(string url)
    {
        if (url.StartsWith("//", StringComparison.Ordinal))
            return true; // protocol-relative: inherits the page's http(s)
        var colon = url.IndexOf(':');
        return colon > 0 && AllowedSchemes.Contains(url[..colon]);
    }
}
