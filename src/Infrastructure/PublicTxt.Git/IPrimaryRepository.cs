namespace PublicTxt.Git;

/// <summary>
/// Git operations for the primary (owned) repository – supports reading and writing.
/// </summary>
public interface IPrimaryRepository : IGitRepository
{
    /// <summary>Initialises a new git repository at <see cref="IGitRepository.LocalPath"/>.</summary>
    void Init();

    /// <summary>Clones <paramref name="remoteUrl"/> into <see cref="IGitRepository.LocalPath"/>.</summary>
    void Clone(string remoteUrl, CloneOptions? options = null);

    /// <summary>Stages all changes (equivalent to <c>git add -A</c>).</summary>
    void StageAll();

    /// <summary>Stages a specific file or directory path.</summary>
    void Stage(string path);

    /// <summary>Creates a commit with the supplied message using the provided author identity.</summary>
    GitCommitInfo Commit(string message, GitIdentity author);

    /// <summary>Fetches from the tracked remote without merging.</summary>
    void Fetch(string remote = "origin");

    /// <summary>Pulls (fetch + merge/rebase) from the tracked remote branch.</summary>
    void Pull(GitIdentity merger, string remote = "origin");

    /// <summary>Pushes committed changes to the tracked remote branch.</summary>
    void Push(string remote = "origin");

    /// <summary>Checks out an existing branch.</summary>
    void Checkout(string branchName);
}
