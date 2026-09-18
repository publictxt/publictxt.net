namespace GitTests;

/// <summary>
/// Helper utilities for Git-related tests.
/// </summary>
internal static class GitTestHelpers
{
    /// <summary>Creates a bare repository at <paramref name="path"/> to act as a remote in tests.</summary>
    public static string CreateBareRepository(string path)
    {
        Directory.CreateDirectory(path);
        LibGit2Sharp.Repository.Init(path, isBare: true);
        return path;
    }

    /// <summary>Returns a unique path under the system temp directory (not created).</summary>
    public static string TempPath(string prefix) =>
        Path.Combine(Path.GetTempPath(), $"publictxt-test-{prefix}-{Guid.NewGuid():N}");

    /// <summary>
    /// Safely deletes a directory, removing read-only attributes first.
    /// This is necessary for Git repositories on Windows which create read-only object files.
    /// </summary>
    /// <param name="path">The directory path to delete.</param>
    public static void DeleteDirectory(string path)
    {
        if (!Directory.Exists(path))
            return;

        // Remove read-only attributes from all files and directories
        // This is necessary for Git repositories on Windows which create read-only object files
        foreach (var dir in Directory.EnumerateDirectories(path, "*", SearchOption.AllDirectories))
        {
            File.SetAttributes(dir, FileAttributes.Normal);
        }

        foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
        {
            File.SetAttributes(file, FileAttributes.Normal);
        }

        Directory.Delete(path, recursive: true);
    }
}
