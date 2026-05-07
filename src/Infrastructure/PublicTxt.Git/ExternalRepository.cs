using LibGit2Sharp;

namespace PublicTxt.Git;

/// <summary>
/// LibGit2Sharp-backed implementation of <see cref="IExternalRepository"/>.
/// Provides read-only access and sync operations for third-party PublicTxt instances.
/// </summary>
public sealed class ExternalRepository : IExternalRepository
{
    public string LocalPath { get; }
    public string? RemoteUrl { get; private set; }

    public ExternalRepository(string localPath, string? remoteUrl = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(localPath);
        LocalPath = localPath;
        RemoteUrl = remoteUrl;
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

    public void CloneOrUpdate(string remoteUrl, CloneOptions? options = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(remoteUrl);
        RemoteUrl = remoteUrl;

        if (IsInitialized)
        {
            Fetch();
            FastForward();
            return;
        }

        Directory.CreateDirectory(LocalPath);
        var cloneOptions = BuildCloneOptions(options);
        Repository.Clone(remoteUrl, LocalPath, cloneOptions);
    }

    public void Fetch(string remote = "origin")
    {
        using var repo = Open();
        var fetchRemote = repo.Network.Remotes[remote]
            ?? throw new InvalidOperationException($"Remote '{remote}' not found.");
        var refSpecs = fetchRemote.FetchRefSpecs.Select(r => r.Specification);
        Commands.Fetch(repo, remote, refSpecs, null, null);
    }

    public void FastForward(string remote = "origin")
    {
        using var repo = Open();
        var trackingBranch = repo.Head.TrackedBranch
            ?? throw new InvalidOperationException(
                $"Branch '{repo.Head.FriendlyName}' has no tracking branch.");

        var mergeResult = repo.Merge(trackingBranch, new Signature("system", "system@localhost", DateTimeOffset.UtcNow),
            new MergeOptions { FastForwardStrategy = FastForwardStrategy.FastForwardOnly });

        if (mergeResult.Status == MergeStatus.Conflicts)
            throw new InvalidOperationException("Fast-forward failed: unexpected conflicts.");
    }

    public string ReadFile(string relativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        var fullPath = Path.Combine(LocalPath, relativePath.TrimStart('/', '\\'));
        if (!File.Exists(fullPath))
            throw new FileNotFoundException($"File not found in repository: {relativePath}", fullPath);
        return File.ReadAllText(fullPath);
    }

    public IEnumerable<string> ListFiles(string? globPattern = null)
    {
        if (!IsInitialized)
            return Enumerable.Empty<string>();

        using var repo = Open();
        var allFiles = repo.Index
            .Select(e => e.Path)
            .OrderBy(p => p);

        if (globPattern is null)
            return allFiles.ToList();

        // Simple glob: support leading ** and * wildcards
        var regex = GlobToRegex(globPattern);
        return allFiles.Where(f => System.Text.RegularExpressions.Regex.IsMatch(f, regex)).ToList();
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

    private static string GlobToRegex(string pattern)
    {
        var escaped = System.Text.RegularExpressions.Regex.Escape(pattern)
            .Replace(@"\*\*", ".*")
            .Replace(@"\*", @"[^/\\]*")
            .Replace(@"\?", ".");
        return $"^{escaped}$";
    }
}
