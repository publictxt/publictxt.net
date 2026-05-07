namespace PublicTxt.Git;

/// <summary>
/// Git operations for external (third-party) repositories – read and sync only, no authoring.
/// </summary>
public interface IExternalRepository : IGitRepository
{
    /// <summary>The remote URL this repository was cloned from.</summary>
    string? RemoteUrl { get; }

    /// <summary>Clones <paramref name="remoteUrl"/> into <see cref="IGitRepository.LocalPath"/>.
    /// Safe to call multiple times; skips the clone if the directory already exists.</summary>
    void CloneOrUpdate(string remoteUrl, CloneOptions? options = null);

    /// <summary>Fetches latest changes from the remote without merging.</summary>
    void Fetch(string remote = "origin");

    /// <summary>Fast-forwards the local branch to match the remote tracking branch.</summary>
    void FastForward(string remote = "origin");

    /// <summary>Reads the raw text content of a file at the given repo-relative path on the current branch.</summary>
    string ReadFile(string relativePath);

    /// <summary>Returns all file paths in the repository that match an optional glob pattern.</summary>
    IEnumerable<string> ListFiles(string? globPattern = null);
}
