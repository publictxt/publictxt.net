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
    /// <exception cref="InvalidOperationException">Nothing is staged.</exception>
    GitCommitInfo Commit(string message, GitIdentity author);

    /// <summary>Pulls (fetch + merge) from the tracked remote branch.</summary>
    /// <exception cref="InvalidOperationException">The remote or the remote branch does not exist.</exception>
    GitMergeResult Pull(GitIdentity merger, string remote = "origin");

    /// <summary>
    /// Pushes the current branch to <paramref name="remote"/>. If the branch does not yet track a
    /// remote branch, upstream tracking is configured so later pulls and tracking status work.
    /// </summary>
    /// <exception cref="InvalidOperationException">The remote does not exist or the branch has no commits.</exception>
    void Push(string remote = "origin");

    /// <summary>
    /// Runs one sync cycle: optionally commit local changes, fetch, merge the remote branch if it
    /// exists, and push if there is anything to publish. Stops before pushing when the merge conflicts.
    /// </summary>
    /// <param name="identity">Author for the commit and any merge commit.</param>
    /// <param name="commitMessage">If supplied and the working tree is dirty, all changes are staged and committed with this message.</param>
    /// <param name="remote">Remote name.</param>
    /// <exception cref="InvalidOperationException">
    /// The working tree is dirty and no <paramref name="commitMessage"/> was given, the remote does not exist,
    /// or the branch has no commits.
    /// </exception>
    GitSyncResult Sync(GitIdentity identity, string? commitMessage = null, string remote = "origin");

    /// <summary>
    /// Pushes a local branch (current branch by default) to <paramref name="remoteBranch"/> on the remote,
    /// creating or updating that branch there. Tracking configuration is left untouched. Used to publish
    /// content to a Pages-style branch.
    /// </summary>
    /// <exception cref="InvalidOperationException">The remote or local branch does not exist, or the branch has no commits.</exception>
    void PushTo(string remote, string remoteBranch, string? localBranch = null);

    /// <summary>Checks out an existing local branch.</summary>
    /// <exception cref="InvalidOperationException">The branch does not exist.</exception>
    void Checkout(string branchName);

    /// <summary>Creates a new local branch from the current HEAD and optionally checks it out.</summary>
    /// <exception cref="InvalidOperationException">The branch already exists or the repository has no commits.</exception>
    void CreateBranch(string branchName, bool checkout = false);

    /// <summary>Adds a remote.</summary>
    /// <exception cref="InvalidOperationException">A remote with that name already exists.</exception>
    void AddRemote(string name, string url);

    /// <summary>Removes a remote.</summary>
    /// <exception cref="InvalidOperationException">The remote does not exist.</exception>
    void RemoveRemote(string name);
}

public enum GitMergeStatus
{
    UpToDate,
    FastForward,
    Conflicts,
    Merged
}

public record GitMergeResult(
    GitMergeStatus Status,
    string? CommitHash,
    IEnumerable<string>? ConflictedFiles = null
);
