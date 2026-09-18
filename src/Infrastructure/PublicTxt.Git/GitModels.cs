namespace PublicTxt.Git;

/// <summary>Immutable snapshot of a single git commit.</summary>
public sealed record GitCommitInfo(
    string Sha,
    string Message,
    GitIdentity Author,
    DateTimeOffset AuthoredAt);

/// <summary>Author/committer identity used when creating commits.</summary>
public sealed record GitIdentity(string Name, string Email);

/// <summary>Summary of the working-directory and index state.</summary>
public sealed record WorkingTreeStatus(
    bool IsClean,
    int StagedCount,
    int UnstagedCount,
    int UntrackedCount,
    int ConflictedCount = 0)
{
    public bool HasConflicts => ConflictedCount > 0;
}

/// <summary>Outcome of a combined commit → fetch → merge → push cycle.</summary>
/// <param name="Committed">The commit created from local changes, or null if nothing was committed.</param>
/// <param name="Pulled">The merge result, or null when the remote branch did not exist yet.</param>
/// <param name="Pushed">Whether a push was performed.</param>
/// <param name="Tracking">Tracking status after the cycle.</param>
public sealed record GitSyncResult(
    GitCommitInfo? Committed,
    GitMergeResult? Pulled,
    bool Pushed,
    TrackingStatus Tracking)
{
    public bool HasConflicts => Pulled?.Status == GitMergeStatus.Conflicts;
}

/// <summary>
/// Relationship between the current branch and its upstream (remote tracking) branch.
/// <see cref="UpstreamBranch"/> is null when the branch tracks nothing.
/// </summary>
public sealed record TrackingStatus(
    string? UpstreamBranch,
    int Ahead,
    int Behind)
{
    public bool IsTracking => UpstreamBranch is not null;
    public bool IsUpToDate => Ahead == 0 && Behind == 0;
    public bool HasDiverged => Ahead > 0 && Behind > 0;

    public static TrackingStatus None { get; } = new(null, 0, 0);
}

/// <summary>A configured remote.</summary>
public sealed record GitRemoteInfo(string Name, string Url);

/// <summary>Options used when cloning a remote repository.</summary>
public sealed record CloneOptions(
    string? BranchName = null,
    bool Checkout = true);
