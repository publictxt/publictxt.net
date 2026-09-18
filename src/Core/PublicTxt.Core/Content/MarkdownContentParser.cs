using System.Text.RegularExpressions;
using Markdig;
using Markdig.Extensions.Yaml;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using YamlDotNet.Serialization;

namespace PublicTxt.Core.Content;

/// <summary>
/// Parses PublicTxt Markdown (md-wiki dialect: standard <c>[text](page.md)</c> links, optional YAML
/// front matter, Obsidian-style inline <c>#tags</c>) into a <see cref="ContentItem"/>.
/// </summary>
public sealed partial class MarkdownContentParser
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseYamlFrontMatter()
        .Build();

    private static readonly IDeserializer Yaml = new DeserializerBuilder()
        .IgnoreUnmatchedProperties()
        .Build();

    // An inline tag: '#' not preceded by a word char, '#', '&' or '/', followed by a tag name that
    // contains at least one letter (so '#123' and '#1' are not tags, matching Obsidian).
    [GeneratedRegex(@"(?<![\w#&/])#(?=[\p{L}\p{N}_/-]*\p{L})([\p{L}\p{N}_/-]+)")]
    private static partial Regex InlineTagPattern();

    [GeneratedRegex(@"^[A-Za-z][A-Za-z0-9+.-]+:")]
    private static partial Regex SchemePattern();

    public static MarkdownContentParser Default { get; } = new();

    /// <summary>Parses a Markdown document into a <see cref="ContentItem"/>.</summary>
    /// <param name="relativePath">Instance-relative path (either slash style is accepted).</param>
    /// <param name="type">Content type the file belongs to.</param>
    /// <param name="markdown">Full file text.</param>
    /// <param name="modifiedAt">File modification time, if known.</param>
    public ContentItem Parse(string relativePath, ContentType type, string markdown, DateTimeOffset? modifiedAt = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        ArgumentNullException.ThrowIfNull(markdown);

        var path = relativePath.Replace('\\', '/').TrimStart('/');
        var document = Markdown.Parse(markdown, Pipeline);

        var frontMatter = ExtractFrontMatter(document, markdown, out var body);
        var heading = document.Descendants<HeadingBlock>().FirstOrDefault(h => h.Level == 1);
        var title = frontMatter.GetString("title")
                    ?? (heading is null ? null : InlineText(heading.Inline))
                    ?? FileNameWithoutExtension(path);

        var tags = MergeTags(frontMatter.GetStringList("tags"), InlineTags(document));
        var links = ExtractLinks(document);

        var date = frontMatter.GetDate("date");
        if (date is null && type == ContentType.Blog && BlogPathConvention.TryGetDate(path, out var pathDate))
            date = pathDate;

        return new ContentItem
        {
            RelativePath = path,
            Type = type,
            Title = title.Trim(),
            FrontMatter = frontMatter,
            Tags = tags,
            Links = links,
            Body = body,
            Date = date,
            ModifiedAt = modifiedAt
        };
    }

    // ── front matter ─────────────────────────────────────────────────────────

    private static FrontMatter ExtractFrontMatter(MarkdownDocument document, string markdown, out string body)
    {
        var block = document.Descendants<YamlFrontMatterBlock>().FirstOrDefault();
        if (block is null)
        {
            body = markdown;
            return FrontMatter.Empty;
        }

        // Body is everything after the closing '---' line.
        var end = Math.Min(block.Span.End + 1, markdown.Length);
        body = markdown[end..].TrimStart('\r', '\n');

        var yaml = string.Join('\n', block.Lines.Lines.Take(block.Lines.Count).Select(l => l.Slice.ToString()));
        if (string.IsNullOrWhiteSpace(yaml))
            return FrontMatter.Empty;

        Dictionary<string, object?>? raw;
        try
        {
            raw = Yaml.Deserialize<Dictionary<string, object?>>(yaml);
        }
        catch (YamlDotNet.Core.YamlException)
        {
            // Malformed front matter: treat as absent rather than failing the whole document.
            return FrontMatter.Empty;
        }

        if (raw is null || raw.Count == 0)
            return FrontMatter.Empty;

        return new FrontMatter(new Dictionary<string, object?>(raw, StringComparer.OrdinalIgnoreCase));
    }

    // ── tags ─────────────────────────────────────────────────────────────────

    private static IEnumerable<string> InlineTags(MarkdownDocument document)
    {
        // Only literal text takes part: code spans, code blocks, link URLs and headings' '#' markers
        // are separate node types and so are naturally skipped.
        foreach (var literal in document.Descendants<LiteralInline>())
        {
            var text = literal.Content.ToString();
            foreach (Match m in InlineTagPattern().Matches(text))
                yield return m.Groups[1].Value.TrimEnd('/', '-');
        }
    }

    private static IReadOnlyList<string> MergeTags(IEnumerable<string> frontMatterTags, IEnumerable<string> inlineTags)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<string>();
        foreach (var raw in frontMatterTags.Concat(inlineTags))
        {
            var tag = raw.Trim().TrimStart('#');
            if (tag.Length == 0 || !seen.Add(tag)) continue;
            result.Add(tag);
        }
        return result;
    }

    // ── links ────────────────────────────────────────────────────────────────

    private static IReadOnlyList<ContentLink> ExtractLinks(MarkdownDocument document)
    {
        var links = new List<ContentLink>();
        foreach (var link in document.Descendants<LinkInline>())
        {
            var url = link.Url;
            if (string.IsNullOrWhiteSpace(url))
                continue;

            var text = InlineText(link);
            links.Add(Classify(url, string.IsNullOrEmpty(text) ? null : text, link.IsImage));
        }
        return links;
    }

    internal static ContentLink Classify(string url, string? text, bool isImage)
    {
        var hash = url.IndexOf('#');
        var fragment = hash >= 0 ? url[(hash + 1)..] : null;
        var target = hash >= 0 ? url[..hash] : url;

        if (target.Length == 0)
            return new ContentLink(string.Empty, text, ContentLinkKind.Anchor, fragment, isImage);

        // A URL scheme needs at least two characters before the colon, which also keeps a Windows
        // drive letter such as "c:\file.md" from being mistaken for one.
        if (SchemePattern().IsMatch(target) || target.StartsWith("//", StringComparison.Ordinal))
            return new ContentLink(target, text, ContentLinkKind.External, fragment, isImage);

        return new ContentLink(target, text, ContentLinkKind.Internal, fragment, isImage);
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private static string? InlineText(ContainerInline? container)
    {
        if (container is null) return null;
        var parts = new List<string>();
        foreach (var inline in container)
        {
            switch (inline)
            {
                case LiteralInline lit: parts.Add(lit.Content.ToString()); break;
                case CodeInline code: parts.Add(code.Content); break;
                case ContainerInline nested: parts.Add(InlineText(nested) ?? string.Empty); break;
            }
        }
        var text = string.Concat(parts).Trim();
        return text.Length == 0 ? null : text;
    }

    private static string FileNameWithoutExtension(string path)
    {
        var name = path[(path.LastIndexOf('/') + 1)..];
        var dot = name.LastIndexOf('.');
        return dot > 0 ? name[..dot] : name;
    }
}
