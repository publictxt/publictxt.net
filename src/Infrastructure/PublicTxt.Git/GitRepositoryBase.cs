using LibGit2Sharp;

namespace PublicTxt.Git;

public abstract class GitRepositoryBase : IGitRepository
{
    public string LocalPath { get; }

    protected GitRepositoryBase(string localPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(localPath);
        LocalPath = localPath;
    }

    public bool IsInitialized =>
        Directory.Exists(LocalPath) && Repository.IsValid(LocalPath);

    public string? CurrentBranch
    {
        get
        {
            if (!IsInitialized) return null;
            using var repo = Open();
            return repo.Head.FriendlyName;
        }
    }

    public GitCommitInfo? LatestCommit
    {
        get
        {
            if (!IsInitialized) return null;
            using var repo = Open();
            var tip = repo.Head.Tip;
            return tip is null ? null : MapCommit(tip);
        }
    }

    public GitRepositoryStatus GetStatus()
    {
        if (!IsInitialized) return GitRepositoryStatus.Clean;
        using var repo = Open();
        var status = repo.RetrieveStatus();
        return new GitRepositoryStatus(
            IsClean: !status.IsDirty,
            StagedCount: status.Staged.Count(),
            UnstagedCount: status.Modified.Count() + status.Missing.Count(),
            UntrackedCount: status.Untracked.Count());
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    protected Repository Open() => new(LocalPath);

    protected static GitCommitInfo MapCommit(Commit commit) =>
        new(
            Sha: commit.Sha,
            Message: commit.MessageShort,
            Author: new GitIdentity(commit.Author.Name, commit.Author.Email),
            AuthoredAt: commit.Author.When);

    protected static LibGit2Sharp.CloneOptions BuildCloneOptions(CloneOptions? options)
    {
        var lo = new LibGit2Sharp.CloneOptions();
        if (options?.BranchName is { } branch)
            lo.BranchName = branch;
        if (options is not null)
            lo.Checkout = options.Checkout;
        return lo;
    }
}
