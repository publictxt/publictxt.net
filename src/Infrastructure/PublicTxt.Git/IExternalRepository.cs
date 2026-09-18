namespace PublicTxt.Git;

/// <summary>
/// Git operations for external (third-party) repositories – read and sync only, no authoring.
/// </summary>
public interface IExternalRepository : IGitRepository
{
    /// <summary>The remote URL this repository was cloned from.</summary>
    string? RemoteUrl { get; }

    /// <summary>Clones <paramref name="remoteUrl"/> into <see cref="IGitRepository.LocalPath"/>.
    /// Safe to call multiple times; if the repository already exists it is fetched and fast-forwarded instead.</summary>
    void CloneOrUpdate(string remoteUrl, CloneOptions? options = null);

    /// <summary>Lists available remote branches (useful for discovering topic branches).</summary>
    IEnumerable<string> GetRemoteBranches(string remote = "origin");

    /// <summary>
    /// Ensures the clone exists and its working tree is at the tip of <c>origin/&lt;branch&gt;</c>
    /// (the remote's default branch when null), fetching first. Unlike <see cref="CloneOrUpdate"/> this
    /// resets rather than fast-forwards, so it copes with force-pushed or switched branches; the clone is
    /// treated as a disposable cache. Returns the resulting commit SHA.
    /// </summary>
    /// <exception cref="InvalidOperationException">The branch does not exist on the remote.</exception>
    string UpdateToBranch(string remoteUrl, string? branch);

    /// <summary>Fast-forwards the local branch to match the remote tracking branch.</summary>
    /// <exception cref="InvalidOperationException">The remote or remote branch does not exist, or the branches have diverged.</exception>
    void FastForward(string remote = "origin");

    /// <summary>Reads the raw text content of a file at the given repo-relative path from the working tree.</summary>
    /// <exception cref="FileNotFoundException">The file does not exist.</exception>
    /// <exception cref="UnauthorizedAccessException">The path escapes the repository root.</exception>
    string ReadFile(string relativePath);

    /// <summary>
    /// Reads the raw text content of a file at the given repo-relative path from the tree of
    /// <paramref name="reference"/> (a branch such as <c>origin/topic/foo</c>, a tag, or a commit SHA),
    /// without checking it out.
    /// </summary>
    /// <exception cref="FileNotFoundException">The file does not exist at that reference.</exception>
    /// <exception cref="InvalidOperationException">The reference cannot be resolved to a commit.</exception>
    string ReadFileAt(string reference, string relativePath);

    /// <summary>
    /// Returns all tracked file paths (forward-slash separated, repo-relative) on the current branch,
    /// optionally filtered by a glob pattern such as <c>**/*.md</c> or <c>blog/**</c>.
    /// </summary>
    IEnumerable<string> ListFiles(string? globPattern = null);

    /// <summary>
    /// Returns all file paths in the tree of <paramref name="reference"/> without checking it out,
    /// optionally filtered by a glob pattern.
    /// </summary>
    /// <exception cref="InvalidOperationException">The reference cannot be resolved to a commit.</exception>
    IEnumerable<string> ListFilesAt(string reference, string? globPattern = null);
}
