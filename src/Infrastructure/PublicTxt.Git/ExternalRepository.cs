using LibGit2Sharp;
using Microsoft.Extensions.FileSystemGlobbing;

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

        // A fast-forward-only merge never creates a commit, so the signature below is never
        // written to history. It is required by the LibGit2Sharp API only.
        var mergeResult = repo.Merge(remoteBranch,
            new Signature("publictxt", "publictxt@localhost", DateTimeOffset.UtcNow),
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

    public string ReadFileAt(string reference, string relativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reference);
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);

        var treePath = NormaliseTreePath(relativePath);

        using var repo = Open();
        var commit = ResolveCommit(repo, reference);
        var entry = commit[treePath];
        if (entry?.TargetType != TreeEntryTargetType.Blob)
            throw new FileNotFoundException($"File not found at '{reference}': {relativePath}", relativePath);

        return ((Blob)entry.Target).GetContentText();
    }

    public IEnumerable<string> ListFiles(string? globPattern = null)
    {
        if (!IsInitialized)
            return Enumerable.Empty<string>();

        using var repo = Open();
        var allFiles = repo.Index.Select(e => e.Path).OrderBy(p => p, StringComparer.Ordinal).ToList();
        return ApplyGlob(allFiles, globPattern);
    }

    public IEnumerable<string> ListFilesAt(string reference, string? globPattern = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reference);

        using var repo = Open();
        var commit = ResolveCommit(repo, reference);
        var allFiles = new List<string>();
        CollectBlobPaths(commit.Tree, allFiles);
        allFiles.Sort(StringComparer.Ordinal);
        return ApplyGlob(allFiles, globPattern);
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private static IReadOnlyList<string> ApplyGlob(IReadOnlyList<string> files, string? globPattern)
    {
        if (string.IsNullOrWhiteSpace(globPattern))
            return files;

        var matcher = new Matcher(StringComparison.OrdinalIgnoreCase);
        matcher.AddInclude(globPattern);
        return matcher.Match(files).Files.Select(f => f.Path).ToList();
    }

    private static void CollectBlobPaths(Tree tree, List<string> paths)
    {
        foreach (var entry in tree)
        {
            switch (entry.TargetType)
            {
                case TreeEntryTargetType.Blob:
                    paths.Add(entry.Path);
                    break;
                case TreeEntryTargetType.Tree:
                    CollectBlobPaths((Tree)entry.Target, paths);
                    break;
            }
        }
    }

    private static Commit ResolveCommit(Repository repo, string reference)
    {
        var obj = repo.Lookup(reference);
        var commit = obj switch
        {
            Commit c => c,
            TagAnnotation t => t.Target.Peel<Commit>(),
            _ => null
        };
        return commit ?? throw new InvalidOperationException($"'{reference}' does not resolve to a commit.");
    }

    /// <summary>Converts an OS-style relative path into a forward-slash tree path and rejects escapes.</summary>
    private static string NormaliseTreePath(string relativePath)
    {
        var normalised = relativePath.Replace('\\', '/').TrimStart('/');
        var segments = normalised.Split('/', StringSplitOptions.RemoveEmptyEntries);

        if (Path.IsPathRooted(relativePath) || segments.Contains(".."))
            throw new UnauthorizedAccessException("Path escapes repository root.");

        return string.Join('/', segments.Where(s => s != "."));
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
