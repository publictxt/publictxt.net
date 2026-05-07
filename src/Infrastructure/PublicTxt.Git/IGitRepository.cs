namespace PublicTxt.Git;

/// <summary>
/// Common read-only git repository operations shared by primary and external repos.
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
    GitRepositoryStatus GetStatus();
}
