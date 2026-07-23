using LibGit2Sharp;

namespace PublicTxt.Git;

/// <summary>
/// LibGit2Sharp-backed implementation of <see cref="IExternalRepository"/>.
/// Provides read-only access and sync operations for third-party PublicTxt instances.
/// </summary>
public sealed class ExternalRepository : GitRepositoryBase, IExternalRepository
{
    public string? RemoteUrl { get; private set; }

    public ExternalRepository(string localPath, string? remoteUrl = null) : base(localPath)
    {
        RemoteUrl = remoteUrl;
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
        Repository.Clone(remoteUrl, LocalPath, BuildCloneOptions(options));
    }

    public void Fetch(string remote = "origin")
    {
        using var repo = Open();
        var fetchRemote = repo.Network.Remotes[remote]
            ?? throw new InvalidOperationException($"Remote '{remote}' not found.");
        var refSpecs = fetchRemote.FetchRefSpecs.Select(r => r.Specification);
        Commands.Fetch(repo, remote, refSpecs, null, null);
    }

    public IEnumerable<string> GetRemoteBranches(string remote = "origin")
    {
        if (!IsInitialized) return Enumerable.Empty<string>();
        using var repo = Open();
        return repo.Branches
            .Where(b => b.IsRemote && b.RemoteName == remote)
            .Select(b => b.FriendlyName)
            .ToList();
    }

    public void FastForward(string remote = "origin")
    {
        using var repo = Open();
        var ffRemote = repo.Network.Remotes[remote]
            ?? throw new InvalidOperationException($"Remote '{remote}' not found.");

        var remoteBranch = repo.Branches[$"{ffRemote.Name}/{repo.Head.FriendlyName}"]
            ?? throw new InvalidOperationException(
                $"Remote branch '{ffRemote.Name}/{repo.Head.FriendlyName}' not found.");

        var mergeResult = repo.Merge(remoteBranch,
            new Signature("system", "system@localhost", DateTimeOffset.UtcNow),
            new MergeOptions { FastForwardStrategy = FastForwardStrategy.FastForwardOnly });

        if (mergeResult.Status == MergeStatus.Conflicts)
            throw new InvalidOperationException("Fast-forward failed: unexpected conflicts.");
    }

    public string ReadFile(string relativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        var fullPath = ResolvePathWithinRepository(relativePath);
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

    private static string GlobToRegex(string pattern)
    {
        var escaped = System.Text.RegularExpressions.Regex.Escape(pattern)
            .Replace(@"\*\*", ".*")
            .Replace(@"\*", @"[^/\\]*")
            .Replace(@"\?", ".");
        return $"^{escaped}$";
    }

    private string ResolvePathWithinRepository(string relativePath)
    {
        var rootPath = Path.GetFullPath(LocalPath);
        var candidatePath = Path.GetFullPath(Path.Combine(rootPath, relativePath));
        var relative = Path.GetRelativePath(rootPath, candidatePath);

        if (relative.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(relative))
            throw new UnauthorizedAccessException("Path escapes repository root.");

        return candidatePath;
    }
}
