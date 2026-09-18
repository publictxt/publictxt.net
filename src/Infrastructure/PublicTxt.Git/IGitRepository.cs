namespace PublicTxt.Git;

/// <summary>
/// Common git repository operations shared by primary and external repos: inspection plus fetch.
/// </summary>
public interface IGitRepository
{
    /// <summary>Gets the path to the local repository on disk.</summary>
    string LocalPath { get; }

    /// <summary>Returns true if a valid git repository exists at <see cref="LocalPath"/>.</summary>
    bool IsInitialized { get; }

    /// <summary>Returns the name of the currently checked-out branch.</summary>
    string? CurrentBranch { get; }

    /// <summary>Returns the most recent commit on the current branch, or null if there are no commits.</summary>
    GitCommitInfo? LatestCommit { get; }

    /// <summary>Returns a summary of the working-directory and index status.</summary>
    /// <exception cref="InvalidOperationException">The repository is not initialized.</exception>
    WorkingTreeStatus GetStatus();

    /// <summary>
    /// Returns how the current branch relates to its upstream branch as of the last fetch.
    /// Call <see cref="Fetch"/> first for an up-to-date answer.
    /// </summary>
    /// <exception cref="InvalidOperationException">The repository is not initialized.</exception>
    TrackingStatus GetTrackingStatus();

    /// <summary>Lists the configured remotes.</summary>
    IReadOnlyList<GitRemoteInfo> GetRemotes();

    /// <summary>Lists local branch names.</summary>
    IReadOnlyList<string> GetLocalBranches();

    /// <summary>Fetches from <paramref name="remote"/> without merging.</summary>
    /// <exception cref="InvalidOperationException">The repository is not initialized or the remote does not exist.</exception>
    void Fetch(string remote = "origin");
}
