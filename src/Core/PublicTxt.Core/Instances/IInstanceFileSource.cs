namespace PublicTxt.Core.Instances;

/// <summary>
/// Read-only view of an instance's files, addressed by instance-relative forward-slash paths
/// (<c>""</c> is the root). Lets Core read an instance from disk, from memory, or from browser storage.
/// </summary>
/// <remarks>
/// Every member throws <see cref="UnauthorizedAccessException"/> when a path escapes the root.
/// Enumeration is non-recursive and unfiltered; walking and skipping hidden entries is the caller's policy.
/// </remarks>
public interface IInstanceFileSource
{
    bool FileExists(string relativePath);

    bool DirectoryExists(string relativePath);

    /// <summary>Immediate child files of a directory as instance-relative paths; empty when it does not exist.</summary>
    IEnumerable<string> EnumerateFiles(string relativeDirectory);

    /// <summary>Immediate child directories as instance-relative paths; empty when it does not exist.</summary>
    IEnumerable<string> EnumerateDirectories(string relativeDirectory);

    string ReadAllText(string relativePath);

    DateTimeOffset GetLastModified(string relativePath);
}
