using LibGit2Sharp;

namespace PublicTxt.Git;

/// <summary>
/// LibGit2Sharp-backed implementation of <see cref="IPrimaryRepository"/>.
/// Manages the primary (owned) git repository for a PublicTxt instance.
/// </summary>
public sealed class PrimaryRepository(string localPath, IGitCredentials? credentials = null)
    : GitRepositoryBase(localPath, credentials), IPrimaryRepository
{
    public void Init()
    {
        Directory.CreateDirectory(LocalPath);
        Repository.Init(LocalPath);
    }

    public void Clone(string remoteUrl, CloneOptions? options = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(remoteUrl);
        Repository.Clone(remoteUrl, LocalPath, BuildCloneOptions(options));
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
        try
        {
            var commit = repo.Commit(message, sig, sig);
            return MapCommit(commit);
        }
        catch (EmptyCommitException ex)
        {
            throw new InvalidOperationException("Nothing to commit: no changes are staged.", ex);
        }
    }

    public GitMergeResult Pull(GitIdentity merger, string remote = "origin")
    {
        Fetch(remote);
        return MergeRemoteBranch(merger, remote)
            ?? throw new InvalidOperationException(
                $"Remote branch '{remote}/{CurrentBranch}' not found.");
    }

    public GitSyncResult Sync(GitIdentity identity, string? commitMessage = null, string remote = "origin")
    {
        EnsureInitialized();

        GitCommitInfo? committed = null;
        var tree = GetStatus();
        if (!tree.IsClean)
        {
            if (commitMessage is null)
                throw new InvalidOperationException(
                    "Working tree has uncommitted changes; supply a commit message or commit first.");
            StageAll();
            committed = Commit(commitMessage, identity);
        }

        Fetch(remote);

        var pulled = MergeRemoteBranch(identity, remote);
        if (pulled?.Status == GitMergeStatus.Conflicts)
            return new GitSyncResult(committed, pulled, Pushed: false, GetTrackingStatus());

        var tracking = GetTrackingStatus();
        var pushed = false;
        if (!tracking.IsTracking || tracking.Ahead > 0)
        {
            Push(remote);
            pushed = true;
        }

        return new GitSyncResult(committed, pulled, pushed, GetTrackingStatus());
    }

    /// <summary>Merges <c>remote/&lt;current branch&gt;</c> into HEAD. Returns null if that remote branch does not exist.</summary>
    private GitMergeResult? MergeRemoteBranch(GitIdentity merger, string remote)
    {
        using var repo = Open();
        RequireRemote(repo, remote);

        var head = repo.Head;
        var remoteBranch = repo.Branches[$"{remote}/{head.FriendlyName}"];
        if (remoteBranch is null)
            return null;

        var sig = new Signature(merger.Name, merger.Email, DateTimeOffset.UtcNow);
        var result = repo.Merge(remoteBranch, sig);

        var status = result.Status switch
        {
            MergeStatus.UpToDate => GitMergeStatus.UpToDate,
            MergeStatus.FastForward => GitMergeStatus.FastForward,
            MergeStatus.Conflicts => GitMergeStatus.Conflicts,
            _ => GitMergeStatus.Merged
        };

        IEnumerable<string>? conflictedFiles = null;
        if (status == GitMergeStatus.Conflicts)
        {
            conflictedFiles = repo.Index.Conflicts
                .Select(c => (c.Ours ?? c.Theirs ?? c.Ancestor)?.Path)
                .Where(p => p is not null)
                .Distinct()
                .ToList()!;
        }

        return new GitMergeResult(status, result.Commit?.Sha, conflictedFiles);
    }

    public void Push(string remote = "origin")
    {
        EnsureInitialized();

        using var repo = Open();
        var pushRemote = RequireRemote(repo, remote);
        var head = repo.Head;
        if (head.Tip is null)
            throw new InvalidOperationException("Nothing to push: the current branch has no commits.");

        repo.Network.Push(pushRemote, head.CanonicalName, BuildPushOptions());

        if (head.TrackedBranch is null || head.RemoteName != remote)
        {
            repo.Branches.Update(head,
                b => b.Remote = remote,
                b => b.UpstreamBranch = head.CanonicalName);
        }
    }

    public void PushTo(string remote, string remoteBranch, string? localBranch = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(remote);
        ArgumentException.ThrowIfNullOrWhiteSpace(remoteBranch);
        EnsureInitialized();

        using var repo = Open();
        var pushRemote = RequireRemote(repo, remote);
        var branch = localBranch is null
            ? repo.Head
            : repo.Branches[localBranch] ?? throw new InvalidOperationException($"Branch '{localBranch}' not found.");

        if (branch.Tip is null)
            throw new InvalidOperationException("Nothing to push: the branch has no commits.");

        repo.Network.Push(pushRemote, $"{branch.CanonicalName}:refs/heads/{remoteBranch}", BuildPushOptions());
    }

    public void Checkout(string branchName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(branchName);
        using var repo = Open();
        var branch = repo.Branches[branchName]
            ?? throw new InvalidOperationException($"Branch '{branchName}' not found.");
        Commands.Checkout(repo, branch);
    }

    public void CreateBranch(string branchName, bool checkout = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(branchName);
        using var repo = Open();

        if (repo.Head.Tip is null)
            throw new InvalidOperationException("Cannot create a branch: the repository has no commits.");
        if (repo.Branches[branchName] is not null)
            throw new InvalidOperationException($"Branch '{branchName}' already exists.");

        var branch = repo.CreateBranch(branchName);
        if (checkout)
            Commands.Checkout(repo, branch);
    }

    public void AddRemote(string name, string url)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(url);
        using var repo = Open();

        if (repo.Network.Remotes[name] is not null)
            throw new InvalidOperationException($"Remote '{name}' already exists.");

        repo.Network.Remotes.Add(name, url);
    }

    public void RemoveRemote(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        using var repo = Open();
        RequireRemote(repo, name);
        repo.Network.Remotes.Remove(name);
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    /// <summary>Hook for credentials and other push options. Null means defaults.</summary>
    private PushOptions? BuildPushOptions()
    {
        var fetchOptions = BuildFetchOptions();
        return fetchOptions?.CredentialsProvider is null
            ? null
            : new PushOptions { CredentialsProvider = fetchOptions.CredentialsProvider };
    }
}
