namespace PublicTxt.Core.Instances;

/// <summary>An instance directory on the local file system.</summary>
public sealed class PhysicalFileSource : IInstanceFileSource
{
    public string RootPath { get; }

    public PhysicalFileSource(string rootPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
        RootPath = Path.GetFullPath(rootPath);
    }

    public bool FileExists(string relativePath) => File.Exists(ToFullPath(relativePath));

    public bool DirectoryExists(string relativePath) => Directory.Exists(ToFullPath(relativePath));

    public IEnumerable<string> EnumerateFiles(string relativeDirectory)
    {
        var full = ToFullPath(relativeDirectory);
        return Directory.Exists(full) ? Directory.EnumerateFiles(full).Select(ToRelativePath) : [];
    }

    public IEnumerable<string> EnumerateDirectories(string relativeDirectory)
    {
        var full = ToFullPath(relativeDirectory);
        return Directory.Exists(full) ? Directory.EnumerateDirectories(full).Select(ToRelativePath) : [];
    }

    public string ReadAllText(string relativePath) => File.ReadAllText(ToFullPath(relativePath));

    public DateTimeOffset GetLastModified(string relativePath) =>
        new(File.GetLastWriteTimeUtc(ToFullPath(relativePath)), TimeSpan.Zero);

    public override string ToString() => RootPath;

    private string ToRelativePath(string fullPath) =>
        Path.GetRelativePath(RootPath, fullPath).Replace('\\', '/');

    private string ToFullPath(string relativePath)
    {
        var candidate = Path.GetFullPath(Path.Combine(RootPath, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        var relative = Path.GetRelativePath(RootPath, candidate);
        if (relative.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(relative))
            throw new UnauthorizedAccessException($"Path escapes the instance root: {relativePath}");
        return candidate;
    }
}
