using PublicTxt.Git;

namespace GitTests;

/// <summary>
/// Exercises PrimaryRepository against a real remote: a local bare repository standing in for origin.
/// Two working copies (A and B) share it so push/fetch/pull interactions can be observed.
/// </summary>
public class PrimaryRepositoryRemoteTests : IDisposable
{
    private static readonly GitIdentity Alice = new("Alice", "alice@example.com");
    private static readonly GitIdentity Bob = new("Bob", "bob@example.com");

    private readonly string _barePath = GitTestHelpers.TempPath("bare");
    private readonly string _pathA = GitTestHelpers.TempPath("a");
    private readonly string _pathB = GitTestHelpers.TempPath("b");

    public PrimaryRepositoryRemoteTests()
    {
        GitTestHelpers.CreateBareRepository(_barePath);
    }

    public void Dispose()
    {
        foreach (var path in new[] { _barePath, _pathA, _pathB })
            GitTestHelpers.DeleteDirectory(path);
    }

    /// <summary>Creates repo A with one commit and pushes it to the bare remote, establishing the default branch there.</summary>
    private PrimaryRepository SeedRemoteViaA()
    {
        var a = new PrimaryRepository(_pathA);
        a.Init();
        WriteAndCommit(a, _pathA, "shared.txt", "v1", "initial", Alice);
        a.AddRemote("origin", _barePath);
        a.Push();
        return a;
    }

    private PrimaryRepository CloneB()
    {
        var b = new PrimaryRepository(_pathB);
        b.Clone(_barePath);
        return b;
    }

    private static GitCommitInfo WriteAndCommit(PrimaryRepository repo, string root, string file, string content, string message, GitIdentity who)
    {
        File.WriteAllText(Path.Combine(root, file), content);
        repo.StageAll();
        return repo.Commit(message, who);
    }

    // ── push / clone / tracking ──────────────────────────────────────────────

    [Fact]
    public void Push_ToBareRemote_SetsUpstreamTracking()
    {
        var a = SeedRemoteViaA();

        var tracking = a.GetTrackingStatus();

        Assert.True(tracking.IsTracking);
        Assert.Equal($"origin/{a.CurrentBranch}", tracking.UpstreamBranch);
        Assert.True(tracking.IsUpToDate);
    }

    [Fact]
    public void Push_Throws_WhenBranchHasNoCommits()
    {
        var a = new PrimaryRepository(_pathA);
        a.Init();
        a.AddRemote("origin", _barePath);

        var ex = Assert.Throws<InvalidOperationException>(() => a.Push());
        Assert.Contains("no commits", ex.Message);
    }

    [Fact]
    public void Clone_FromBareRemote_IsInitializedAndTracking()
    {
        SeedRemoteViaA();

        var b = CloneB();

        Assert.True(b.IsInitialized);
        Assert.Equal("initial", b.LatestCommit!.Message);
        Assert.Equal("v1", File.ReadAllText(Path.Combine(_pathB, "shared.txt")));
        Assert.True(b.GetTrackingStatus().IsTracking);
        Assert.Contains(b.GetRemotes(), r => r.Name == "origin" && r.Url == _barePath);
    }

    [Fact]
    public void GetTrackingStatus_ReportsAhead_AfterLocalCommit()
    {
        SeedRemoteViaA();
        var b = CloneB();

        WriteAndCommit(b, _pathB, "b-only.txt", "b", "b commit", Bob);

        var tracking = b.GetTrackingStatus();
        Assert.Equal(1, tracking.Ahead);
        Assert.Equal(0, tracking.Behind);
    }

    [Fact]
    public void Fetch_ThenTrackingStatus_ReportsBehind_WhenRemoteAdvanced()
    {
        var a = SeedRemoteViaA();
        var b = CloneB();

        WriteAndCommit(a, _pathA, "a-only.txt", "a", "a commit", Alice);
        a.Push();

        Assert.True(b.GetTrackingStatus().IsUpToDate, "before fetch, B should not know about A's push");
        b.Fetch();

        var tracking = b.GetTrackingStatus();
        Assert.Equal(0, tracking.Ahead);
        Assert.Equal(1, tracking.Behind);
        Assert.False(File.Exists(Path.Combine(_pathB, "a-only.txt")), "fetch must not touch the working tree");
    }

    [Fact]
    public void GetTrackingStatus_ReportsDiverged_WhenBothSidesCommitted()
    {
        var a = SeedRemoteViaA();
        var b = CloneB();

        WriteAndCommit(a, _pathA, "a-only.txt", "a", "a commit", Alice);
        a.Push();
        WriteAndCommit(b, _pathB, "b-only.txt", "b", "b commit", Bob);
        b.Fetch();

        var tracking = b.GetTrackingStatus();
        Assert.True(tracking.HasDiverged);
        Assert.Equal(1, tracking.Ahead);
        Assert.Equal(1, tracking.Behind);
    }

    // ── pull ────────────────────────────────────────────────────────────────

    [Fact]
    public void Pull_ReturnsUpToDate_WhenNothingChanged()
    {
        SeedRemoteViaA();
        var b = CloneB();

        var result = b.Pull(Bob);

        Assert.Equal(GitMergeStatus.UpToDate, result.Status);
    }

    [Fact]
    public void Pull_FastForwards_WhenOnlyRemoteAdvanced()
    {
        var a = SeedRemoteViaA();
        var b = CloneB();

        var pushed = WriteAndCommit(a, _pathA, "a-only.txt", "a", "a commit", Alice);
        a.Push();

        var result = b.Pull(Bob);

        Assert.Equal(GitMergeStatus.FastForward, result.Status);
        Assert.Equal(pushed.Sha, b.LatestCommit!.Sha);
        Assert.Equal("a", File.ReadAllText(Path.Combine(_pathB, "a-only.txt")));
        Assert.True(b.GetTrackingStatus().IsUpToDate);
    }

    [Fact]
    public void Pull_CreatesMergeCommit_WhenBothSidesChangedDifferentFiles()
    {
        var a = SeedRemoteViaA();
        var b = CloneB();

        WriteAndCommit(a, _pathA, "a-only.txt", "a", "a commit", Alice);
        a.Push();
        WriteAndCommit(b, _pathB, "b-only.txt", "b", "b commit", Bob);

        var result = b.Pull(Bob);

        Assert.Equal(GitMergeStatus.Merged, result.Status);
        Assert.NotNull(result.CommitHash);
        Assert.Equal(result.CommitHash, b.LatestCommit!.Sha);
        Assert.True(File.Exists(Path.Combine(_pathB, "a-only.txt")));
        Assert.True(File.Exists(Path.Combine(_pathB, "b-only.txt")));
        Assert.True(b.GetStatus().IsClean);
    }

    [Fact]
    public void Pull_ReportsConflicts_WhenSameFileChangedOnBothSides()
    {
        var a = SeedRemoteViaA();
        var b = CloneB();

        WriteAndCommit(a, _pathA, "shared.txt", "from A", "a edit", Alice);
        a.Push();
        WriteAndCommit(b, _pathB, "shared.txt", "from B", "b edit", Bob);

        var result = b.Pull(Bob);

        Assert.Equal(GitMergeStatus.Conflicts, result.Status);
        Assert.Null(result.CommitHash);
        Assert.NotNull(result.ConflictedFiles);
        Assert.Contains("shared.txt", result.ConflictedFiles!);
        Assert.False(b.GetStatus().IsClean);
    }

    [Fact]
    public void Pull_Throws_WhenRemoteBranchDoesNotExist()
    {
        SeedRemoteViaA();
        var b = CloneB();
        b.CreateBranch("topic/new", checkout: true);

        var ex = Assert.Throws<InvalidOperationException>(() => b.Pull(Bob));
        Assert.Contains("origin/topic/new", ex.Message);
    }

    // ── round trip ──────────────────────────────────────────────────────────

    [Fact]
    public void Push_AfterPull_PublishesMergedHistory()
    {
        var a = SeedRemoteViaA();
        var b = CloneB();

        WriteAndCommit(a, _pathA, "a-only.txt", "a", "a commit", Alice);
        a.Push();
        WriteAndCommit(b, _pathB, "b-only.txt", "b", "b commit", Bob);
        b.Pull(Bob);
        b.Push();

        var pulled = a.Pull(Alice);

        Assert.Equal(GitMergeStatus.FastForward, pulled.Status);
        Assert.True(File.Exists(Path.Combine(_pathA, "b-only.txt")));
        Assert.True(a.GetTrackingStatus().IsUpToDate);
        Assert.True(b.GetTrackingStatus().IsUpToDate);
    }

    [Fact]
    public void Push_NewBranch_CreatesItOnRemoteAndTracksIt()
    {
        var a = SeedRemoteViaA();
        a.CreateBranch("topic/recipes", checkout: true);
        WriteAndCommit(a, _pathA, "recipes.md", "# Recipes", "recipes", Alice);

        a.Push();

        Assert.Equal("origin/topic/recipes", a.GetTrackingStatus().UpstreamBranch);
        var ext = new ExternalRepository(_pathB);
        ext.CloneOrUpdate(_barePath);
        Assert.Contains("origin/topic/recipes", ext.GetRemoteBranches());
        Assert.Equal("# Recipes", ext.ReadFileAt("origin/topic/recipes", "recipes.md"));
    }
}
