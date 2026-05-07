using LibGit2Sharp;

namespace PublicTxt.Git;

/// <summary>
/// LibGit2Sharp-backed implementation of <see cref="IPrimaryRepository"/>.
/// Manages the primary (owned) git repository for a PublicTxt instance.
/// </summary>
public sealed class PrimaryRepository : IPrimaryRepository
{
    public string LocalPath { get; }

    public PrimaryRepository(string localPath)
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

    public void Init()
    {
        Directory.CreateDirectory(LocalPath);
        LibGit2Sharp.Repository.Init(LocalPath);
    }

    public void Clone(string remoteUrl, CloneOptions? options = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(remoteUrl);
        var cloneOptions = BuildCloneOptions(options);
        LibGit2Sharp.Repository.Clone(remoteUrl, LocalPath, cloneOptions);
    }

    public void StageAll()
    {
        using var repo = Open();
        Commands.Stage(repo, "*");
    }

    public void Stage(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        using var repo = Open();
        Commands.Stage(repo, path);
    }

    public GitCommitInfo Commit(string message, GitIdentity author)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        using var repo = Open();
        var sig = new Signature(author.Name, author.Email, DateTimeOffset.UtcNow);
        var commit = repo.Commit(message, sig, sig);
        return MapCommit(commit);
    }

    public void Fetch(string remote = "origin")
    {
        using var repo = Open();
        var fetchRemote = repo.Network.Remotes[remote]
            ?? throw new InvalidOperationException($"Remote '{remote}' not found.");
        var refSpecs = fetchRemote.FetchRefSpecs.Select(r => r.Specification);
        Commands.Fetch(repo, remote, refSpecs, null, null);
    }

    public void Pull(GitIdentity merger, string remote = "origin")
    {
        using var repo = Open();
        var sig = new Signature(merger.Name, merger.Email, DateTimeOffset.UtcNow);
        var pullOptions = new LibGit2Sharp.PullOptions
        {
            FetchOptions = new FetchOptions()
        };
        Commands.Pull(repo, sig, pullOptions);
    }

    public void Push(string remote = "origin")
    {
        using var repo = Open();
        var pushRemote = repo.Network.Remotes[remote]
            ?? throw new InvalidOperationException($"Remote '{remote}' not found.");
        repo.Network.Push(pushRemote, repo.Head.CanonicalName, (LibGit2Sharp.PushOptions?)null);
    }

    public void Checkout(string branchName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(branchName);
        using var repo = Open();
        var branch = repo.Branches[branchName]
            ?? throw new InvalidOperationException($"Branch '{branchName}' not found.");
        Commands.Checkout(repo, branch);
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private Repository Open() => new(LocalPath);

    private static GitCommitInfo MapCommit(Commit commit) =>
        new(
            Sha: commit.Sha,
            Message: commit.MessageShort,
            Author: new GitIdentity(commit.Author.Name, commit.Author.Email),
            AuthoredAt: commit.Author.When);

    private static LibGit2Sharp.CloneOptions BuildCloneOptions(CloneOptions? options)
    {
        var lo = new LibGit2Sharp.CloneOptions();
        if (options?.BranchName is { } branch)
            lo.BranchName = branch;
        if (options is not null)
            lo.Checkout = options.Checkout;
        return lo;
    }
}
