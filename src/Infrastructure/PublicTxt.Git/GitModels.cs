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
    int UntrackedCount);

/// <summary>Options used when cloning a remote repository.</summary>
public sealed record CloneOptions(
    string? BranchName = null,
    bool Checkout = true);
