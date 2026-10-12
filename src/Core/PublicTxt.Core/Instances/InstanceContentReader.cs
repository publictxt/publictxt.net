using PublicTxt.Core.Content;
using PublicTxt.Core.Models;

namespace PublicTxt.Core.Instances;

/// <summary>Enumerates and parses the content of an instance according to its settings.</summary>
public sealed class InstanceContentReader
{
    private static readonly string[] MarkdownExtensions = [".md", ".markdown"];

    private readonly MarkdownContentParser _parser;
    private readonly IReadOnlyDictionary<ContentType, string> _paths;

    public IInstanceFileSource Source { get; }
    public TxtInstanceSettings Settings { get; }

    public InstanceContentReader(string rootPath, TxtInstanceSettings settings, MarkdownContentParser? parser = null)
        : this(new PhysicalFileSource(rootPath), settings, parser)
    {
    }

    public InstanceContentReader(IInstanceFileSource source, TxtInstanceSettings settings, MarkdownContentParser? parser = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(settings);

        Source = source;
        Settings = settings;
        _parser = parser ?? MarkdownContentParser.Default;
        _paths = InstanceLayout.ContentPaths(settings);
    }

    /// <summary>Opens the instance at <paramref name="rootPath"/>, loading its settings file (or defaults).</summary>
    public static InstanceContentReader Open(string rootPath, MarkdownContentParser? parser = null) =>
        Open(new PhysicalFileSource(rootPath), parser);

    /// <summary>Opens the instance held by <paramref name="source"/>, loading its settings file (or defaults).</summary>
    public static InstanceContentReader Open(IInstanceFileSource source, MarkdownContentParser? parser = null) =>
        new(source, new TxtInstanceSettingsStore().Load(source), parser);

    /// <summary>
    /// All files (any extension) under a content type's directory as instance-relative forward-slash
    /// paths. Dot-prefixed files and directories (e.g. <c>.obsidian</c>, <c>.gitkeep</c>) are skipped.
    /// </summary>
    public IEnumerable<string> EnumerateFiles(ContentType type)
    {
        if (!_paths.TryGetValue(type, out var dir))
            return Enumerable.Empty<string>();

        return Walk(dir).OrderBy(p => p, StringComparer.Ordinal).ToList();
    }

    /// <summary>Parsed Markdown items of one content type.</summary>
    public IEnumerable<ContentItem> Enumerate(ContentType type)
    {
        if (!type.IsMarkdownContent())
            yield break;

        foreach (var relativePath in EnumerateFiles(type))
        {
            if (!IsMarkdown(relativePath)) continue;
            yield return ParseFile(relativePath, type);
        }
    }

    /// <summary>Parsed Markdown items across every Markdown content type.</summary>
    public IEnumerable<ContentItem> EnumerateAll()
    {
        foreach (var type in _paths.Keys.Where(t => t.IsMarkdownContent()))
            foreach (var item in Enumerate(type))
                yield return item;
    }

    /// <summary>Reads and parses one Markdown file, or returns null if it does not exist or is not Markdown.</summary>
    /// <exception cref="UnauthorizedAccessException">The path escapes the instance root.</exception>
    public ContentItem? Read(string relativePath)
    {
        var rel = InstanceLayout.Normalise(relativePath);
        if (!IsMarkdown(rel) || !FileExists(rel))
            return null;

        return ParseFile(rel, InstanceLayout.TypeOf(Settings, rel));
    }

    /// <summary>True when a file exists at the instance-relative path.</summary>
    /// <exception cref="UnauthorizedAccessException">The path escapes the instance root.</exception>
    public bool FileExists(string relativePath) => Source.FileExists(relativePath);

    /// <summary>Builds a catalog of every item plus resolved links.</summary>
    public ContentCatalog BuildCatalog() => ContentCatalog.Build(this);

    // ── helpers ──────────────────────────────────────────────────────────────

    private ContentItem ParseFile(string relativePath, ContentType type) =>
        _parser.Parse(relativePath, type, Source.ReadAllText(relativePath), Source.GetLastModified(relativePath));

    private static bool IsMarkdown(string path) =>
        MarkdownExtensions.Any(ext => path.EndsWith(ext, StringComparison.OrdinalIgnoreCase));

    private IEnumerable<string> Walk(string directory)
    {
        foreach (var file in Source.EnumerateFiles(directory))
        {
            if (!IsHidden(file))
                yield return file;
        }

        foreach (var sub in Source.EnumerateDirectories(directory))
        {
            if (IsHidden(sub)) continue;
            foreach (var file in Walk(sub))
                yield return file;
        }
    }

    private static bool IsHidden(string relativePath) =>
        relativePath[(relativePath.LastIndexOf('/') + 1)..].StartsWith('.');
}
