namespace PublicTxt.Core.Instances;

/// <summary>
/// Instance files held in memory, e.g. loaded from browser storage. Paths are case-sensitive, as in git.
/// Directories exist implicitly when they contain a file. Binary files (media) only need to be present
/// for link resolution, so add them with empty text.
/// </summary>
public sealed class InMemoryFileSource : IInstanceFileSource
{
    private readonly Dictionary<string, (string Text, DateTimeOffset Modified)> _files = new(StringComparer.Ordinal);

    public int Count => _files.Count;

    /// <summary>Adds or replaces a file.</summary>
    public void Add(string relativePath, string text, DateTimeOffset lastModified = default)
    {
        ArgumentNullException.ThrowIfNull(text);
        var key = Key(relativePath);
        if (key.Length == 0)
            throw new ArgumentException("A file path is required.", nameof(relativePath));
        _files[key] = (text, lastModified);
    }

    public bool Remove(string relativePath) => _files.Remove(Key(relativePath));

    public bool FileExists(string relativePath) => _files.ContainsKey(Key(relativePath));

    public bool DirectoryExists(string relativePath)
    {
        var prefix = Prefix(Key(relativePath));
        return prefix.Length == 0 || _files.Keys.Any(k => k.StartsWith(prefix, StringComparison.Ordinal));
    }

    public IEnumerable<string> EnumerateFiles(string relativeDirectory)
    {
        var prefix = Prefix(Key(relativeDirectory));
        return _files.Keys
            .Where(k => k.StartsWith(prefix, StringComparison.Ordinal) && k.IndexOf('/', prefix.Length) < 0)
            .ToList();
    }

    public IEnumerable<string> EnumerateDirectories(string relativeDirectory)
    {
        var prefix = Prefix(Key(relativeDirectory));
        return _files.Keys
            .Where(k => k.StartsWith(prefix, StringComparison.Ordinal))
            .Select(k => k.IndexOf('/', prefix.Length) is var slash and >= 0 ? k[..slash] : null)
            .OfType<string>()
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    public string ReadAllText(string relativePath) =>
        _files.TryGetValue(Key(relativePath), out var file)
            ? file.Text
            : throw new FileNotFoundException($"No such file in memory: {relativePath}", relativePath);

    public DateTimeOffset GetLastModified(string relativePath) =>
        _files.TryGetValue(Key(relativePath), out var file)
            ? file.Modified
            : throw new FileNotFoundException($"No such file in memory: {relativePath}", relativePath);

    public override string ToString() => $"in-memory instance ({Count} files)";

    private static string Prefix(string directoryKey) => directoryKey.Length == 0 ? "" : directoryKey + "/";

    /// <summary>Normalises to forward slashes and resolves <c>.</c>/<c>..</c>; rooted or escaping paths throw.</summary>
    private static string Key(string relativePath)
    {
        var path = (relativePath ?? string.Empty).Replace('\\', '/');
        if (path.StartsWith('/'))
            throw new UnauthorizedAccessException($"Path escapes the instance root: {relativePath}");

        var segments = new List<string>();
        foreach (var segment in path.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (segment == ".") continue;
            if (segment == "..")
            {
                if (segments.Count == 0)
                    throw new UnauthorizedAccessException($"Path escapes the instance root: {relativePath}");
                segments.RemoveAt(segments.Count - 1);
                continue;
            }
            segments.Add(segment);
        }
        return string.Join('/', segments);
    }
}
