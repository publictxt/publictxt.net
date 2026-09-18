using LibGit2Sharp;

namespace PublicTxt.Git;

public abstract class GitRepositoryBase : IGitRepository
{
    public string LocalPath { get; }

    /// <summary>Credential source used for fetch, pull, push and clone. Null means anonymous/default.</summary>
    protected IGitCredentials? Credentials { get; }

    protected GitRepositoryBase(string localPath, IGitCredentials? credentials = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(localPath);
        LocalPath = localPath;
        Credentials = credentials;
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

    public WorkingTreeStatus GetStatus()
    {
        EnsureInitialized();

        using var repo = Open();
        var status = repo.RetrieveStatus();
        return new WorkingTreeStatus(
            IsClean: !status.IsDirty,
            StagedCount: status.Staged.Count(),
            UnstagedCount: status.Modified.Count() + status.Missing.Count(),
            UntrackedCount: status.Untracked.Count());
    }

    public TrackingStatus GetTrackingStatus()
    {
        EnsureInitialized();

        using var repo = Open();
        var head = repo.Head;
        var upstream = head.TrackedBranch;
        if (upstream is null)
            return TrackingStatus.None;

        var details = head.TrackingDetails;
        return new TrackingStatus(
            UpstreamBranch: upstream.FriendlyName,
            Ahead: details.AheadBy ?? 0,
            Behind: details.BehindBy ?? 0);
    }

    public IReadOnlyList<GitRemoteInfo> GetRemotes()
    {
        if (!IsInitialized) return Array.Empty<GitRemoteInfo>();
        using var repo = Open();
        return repo.Network.Remotes
            .Select(r => new GitRemoteInfo(r.Name, r.Url))
            .ToList();
    }

    public IReadOnlyList<string> GetLocalBranches()
    {
        if (!IsInitialized) return Array.Empty<string>();
        using var repo = Open();
        return repo.Branches
            .Where(b => !b.IsRemote)
            .Select(b => b.FriendlyName)
            .ToList();
    }

    public void Fetch(string remote = "origin")
    {
        EnsureInitialized();

        using var repo = Open();
        var fetchRemote = RequireRemote(repo, remote);
        var refSpecs = fetchRemote.FetchRefSpecs.Select(r => r.Specification);
        Commands.Fetch(repo, remote, refSpecs, BuildFetchOptions(), null);
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    protected Repository Open() => new(LocalPath);

    protected void EnsureInitialized()
    {
        if (!IsInitialized)
            throw new InvalidOperationException($"Repository at '{LocalPath}' is not initialized.");
    }

    protected static Remote RequireRemote(Repository repo, string name) =>
        repo.Network.Remotes[name]
        ?? throw new InvalidOperationException($"Remote '{name}' not found.");

    /// <summary>Fetch options carrying the configured credentials, or null when running anonymously.</summary>
    protected FetchOptions? BuildFetchOptions()
    {
        var handler = GitCredentialAdapter.ToHandler(Credentials);
        return handler is null ? null : new FetchOptions { CredentialsProvider = handler };
    }

    protected static GitCommitInfo MapCommit(Commit commit) =>
        new(
            Sha: commit.Sha,
            Message: commit.MessageShort,
            Author: new GitIdentity(commit.Author.Name, commit.Author.Email),
            AuthoredAt: commit.Author.When);

    protected LibGit2Sharp.CloneOptions BuildCloneOptions(CloneOptions? options)
    {
        var lo = new LibGit2Sharp.CloneOptions();
        if (options?.BranchName is { } branch)
            lo.BranchName = branch;
        if (options is not null)
            lo.Checkout = options.Checkout;
        if (BuildFetchOptions() is { } fetchOptions)
            lo.FetchOptions.CredentialsProvider = fetchOptions.CredentialsProvider;
        return lo;
    }
}
