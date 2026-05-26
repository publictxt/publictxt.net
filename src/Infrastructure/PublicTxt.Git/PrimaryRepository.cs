using LibGit2Sharp;

namespace PublicTxt.Git;

/// <summary>
/// LibGit2Sharp-backed implementation of <see cref="IPrimaryRepository"/>.
/// Manages the primary (owned) git repository for a PublicTxt instance.
/// </summary>
public sealed class PrimaryRepository(string localPath) : GitRepositoryBase(localPath), IPrimaryRepository
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

    public GitMergeResult Pull(GitIdentity merger, string remote = "origin")
    {
        using var repo = Open();
        var pullRemote = repo.Network.Remotes[remote]
            ?? throw new InvalidOperationException($"Remote '{remote}' not found.");

        var refSpecs = pullRemote.FetchRefSpecs.Select(r => r.Specification);
        Commands.Fetch(repo, remote, refSpecs, null, null);

        var head = repo.Head;
        var remoteBranch = repo.Branches[$"{remote}/{head.FriendlyName}"]
            ?? throw new InvalidOperationException(
                $"Remote branch '{remote}/{head.FriendlyName}' not found.");

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
            conflictedFiles = repo.Index.Conflicts.Select(c => c.Ours.Path).Distinct().ToList();

        return new GitMergeResult(status, result.Commit?.Sha, conflictedFiles);
    }

    public void Push(string remote = "origin")
    {
        using var repo = Open();
        var pushRemote = repo.Network.Remotes[remote]
            ?? throw new InvalidOperationException($"Remote '{remote}' not found.");
        repo.Network.Push(pushRemote, repo.Head.CanonicalName, (PushOptions?)null);
    }

    public void Checkout(string branchName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(branchName);
        using var repo = Open();
        var branch = repo.Branches[branchName]
            ?? throw new InvalidOperationException($"Branch '{branchName}' not found.");
        Commands.Checkout(repo, branch);
    }
}
