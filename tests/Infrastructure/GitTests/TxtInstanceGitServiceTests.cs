using PublicTxt.Core.Models;
using PublicTxt.Git;

namespace GitTests;

public class TxtInstanceGitServiceTests : IDisposable
{
    private static readonly GitIdentity Alice = new("Alice", "alice@example.com");
    private static readonly GitIdentity Bob = new("Bob", "bob@example.com");

    private readonly string _barePath = GitTestHelpers.TempPath("svc-bare");
    private readonly string _seedPath = GitTestHelpers.TempPath("svc-seed");
    private readonly string _instancePath = GitTestHelpers.TempPath("svc-instance");
    private readonly TxtInstanceGitService _service = new();

    public void Dispose()
    {
        foreach (var p in new[] { _barePath, _seedPath, _instancePath })
            GitTestHelpers.DeleteDirectory(p);
    }

    private TxtInstance NewInstance(string? remoteUrl = null) => new()
    {
        Name = "test",
        LocalPath = _instancePath,
        RemoteUrl = remoteUrl
    };

    /// <summary>Creates the bare remote with one commit pushed from a separate seed working copy.</summary>
    private PrimaryRepository SeedRemote()
    {
        GitTestHelpers.CreateBareRepository(_barePath);
        var seed = new PrimaryRepository(_seedPath);
        seed.Init();
        File.WriteAllText(Path.Combine(_seedPath, "wiki.md"), "# Wiki");
        seed.StageAll();
        seed.Commit("seed", Alice);
        seed.AddRemote("origin", _barePath);
        seed.Push();
        return seed;
    }

    // ── OpenRepository ───────────────────────────────────────────────────────

    [Fact]
    public void OpenRepository_Throws_WithoutLocalPath()
    {
        var instance = new TxtInstance { Name = "no-path" };
        Assert.Throws<InvalidOperationException>(() => _service.OpenRepository(instance));
    }

    // ── Initialize ───────────────────────────────────────────────────────────

    [Fact]
    public void Initialize_LocalOnly_CreatesRepositoryAndMarksReady()
    {
        var instance = NewInstance();

        var repo = _service.Initialize(instance);

        Assert.True(repo.IsInitialized);
        Assert.Equal(InstanceStatus.Ready, instance.Status);
        Assert.Equal(GitStatus.Unknown, instance.GitStatus);
        Assert.Equal(repo.CurrentBranch, instance.CurrentBranch);
        Assert.NotNull(instance.LastAccessedAt);
        Assert.Empty(repo.GetRemotes());
    }

    [Fact]
    public void Initialize_WithRemoteUrl_ClonesAndReportsSynced()
    {
        SeedRemote();
        var instance = NewInstance(_barePath);

        var repo = _service.Initialize(instance);

        Assert.True(repo.IsInitialized);
        Assert.Equal("# Wiki", File.ReadAllText(Path.Combine(_instancePath, "wiki.md")));
        Assert.Equal(InstanceStatus.Ready, instance.Status);
        Assert.Equal(GitStatus.Synced, instance.GitStatus);
        Assert.Equal(repo.CurrentBranch, instance.CurrentBranch);
    }

    [Fact]
    public void Initialize_IsIdempotent()
    {
        SeedRemote();
        var instance = NewInstance(_barePath);
        var first = _service.Initialize(instance).LatestCommit!.Sha;

        var second = _service.Initialize(instance).LatestCommit!.Sha;

        Assert.Equal(first, second);
        Assert.Equal(InstanceStatus.Ready, instance.Status);
    }

    [Fact]
    public void Initialize_ExistingLocalRepo_WithRemoteUrl_AddsOrigin()
    {
        var instance = NewInstance();
        _service.Initialize(instance);
        GitTestHelpers.CreateBareRepository(_barePath);

        instance.RemoteUrl = _barePath;
        var repo = _service.Initialize(instance);

        var origin = Assert.Single(repo.GetRemotes());
        Assert.Equal("origin", origin.Name);
        Assert.Equal(_barePath, origin.Url);
    }

    [Fact]
    public void Initialize_OnFailure_MarksError()
    {
        var instance = NewInstance(Path.Combine(Path.GetTempPath(), $"does-not-exist-{Guid.NewGuid():N}"));

        Assert.ThrowsAny<Exception>(() => _service.Initialize(instance));

        Assert.Equal(InstanceStatus.Error, instance.Status);
        Assert.Equal(GitStatus.Error, instance.GitStatus);
    }

    // ── Refresh ──────────────────────────────────────────────────────────────

    [Fact]
    public void Refresh_ReportsLocalChanges_WhenTreeDirty()
    {
        SeedRemote();
        var instance = NewInstance(_barePath);
        _service.Initialize(instance);

        File.WriteAllText(Path.Combine(_instancePath, "new.md"), "x");
        _service.Refresh(instance);

        Assert.Equal(GitStatus.LocalChanges, instance.GitStatus);
    }

    [Fact]
    public void Refresh_WithFetch_ReportsRemoteChanges()
    {
        var seed = SeedRemote();
        var instance = NewInstance(_barePath);
        _service.Initialize(instance);

        File.WriteAllText(Path.Combine(_seedPath, "more.md"), "y");
        seed.StageAll();
        seed.Commit("more", Alice);
        seed.Push();

        _service.Refresh(instance, fetch: false);
        Assert.Equal(GitStatus.Synced, instance.GitStatus);

        _service.Refresh(instance, fetch: true);
        Assert.Equal(GitStatus.RemoteChanges, instance.GitStatus);
    }

    [Fact]
    public void Refresh_OnUninitializedPath_MarksUninitialized()
    {
        var instance = NewInstance();
        instance.Status = InstanceStatus.Ready;

        _service.Refresh(instance);

        Assert.Equal(InstanceStatus.Uninitialized, instance.Status);
        Assert.Equal(GitStatus.Unknown, instance.GitStatus);
    }

    // ── Sync ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Sync_CommitsPushesAndUpdatesInstance()
    {
        SeedRemote();
        var instance = NewInstance(_barePath);
        _service.Initialize(instance);
        File.WriteAllText(Path.Combine(_instancePath, "post.md"), "# Post");
        var before = DateTimeOffset.UtcNow.AddSeconds(-1);

        var result = _service.Sync(instance, Bob, "add post");

        Assert.NotNull(result.Committed);
        Assert.Equal("add post", result.Committed!.Message);
        Assert.Equal(GitMergeStatus.UpToDate, result.Pulled!.Status);
        Assert.True(result.Pushed);
        Assert.True(result.Tracking.IsUpToDate);
        Assert.Equal(InstanceStatus.Ready, instance.Status);
        Assert.Equal(GitStatus.Synced, instance.GitStatus);
        Assert.True(instance.LastGitSync >= before);

        var check = new ExternalRepository(GitTestHelpers.TempPath("svc-check"));
        try
        {
            check.CloneOrUpdate(_barePath);
            Assert.Equal("# Post", check.ReadFile("post.md"));
        }
        finally
        {
            GitTestHelpers.DeleteDirectory(check.LocalPath);
        }
    }

    [Fact]
    public void Sync_WithConflicts_MarksConflicted_AndDoesNotPush()
    {
        var seed = SeedRemote();
        var instance = NewInstance(_barePath);
        _service.Initialize(instance);

        File.WriteAllText(Path.Combine(_seedPath, "wiki.md"), "# Wiki (seed)");
        seed.StageAll();
        seed.Commit("seed edit", Alice);
        seed.Push();
        File.WriteAllText(Path.Combine(_instancePath, "wiki.md"), "# Wiki (instance)");

        var result = _service.Sync(instance, Bob, "instance edit");

        Assert.True(result.HasConflicts);
        Assert.False(result.Pushed);
        Assert.Contains("wiki.md", result.Pulled!.ConflictedFiles!);
        Assert.Equal(GitStatus.Conflicted, instance.GitStatus);
        Assert.Equal(InstanceStatus.Ready, instance.Status);
    }

    [Fact]
    public void Sync_OnFailure_MarksError()
    {
        var instance = NewInstance();
        // Not initialised: Sync must fail.

        Assert.Throws<InvalidOperationException>(() => _service.Sync(instance, Bob));

        Assert.Equal(InstanceStatus.Error, instance.Status);
        Assert.Equal(GitStatus.Error, instance.GitStatus);
    }

    // ── ComputeGitStatus ─────────────────────────────────────────────────────

    public static TheoryData<bool, string?, int, int, int, GitStatus> StatusCases => new()
    {
        // clean, upstream, ahead, behind, conflicts, expected
        { true,  null,          0, 0, 0, GitStatus.Unknown },
        { false, null,          0, 0, 0, GitStatus.LocalChanges },
        { true,  "origin/main", 0, 0, 0, GitStatus.Synced },
        { false, "origin/main", 0, 0, 0, GitStatus.LocalChanges },
        { true,  "origin/main", 2, 0, 0, GitStatus.LocalChanges },
        { true,  "origin/main", 0, 3, 0, GitStatus.RemoteChanges },
        { true,  "origin/main", 1, 1, 0, GitStatus.Diverged },
        { false, "origin/main", 0, 1, 0, GitStatus.Diverged },
        { false, "origin/main", 0, 0, 1, GitStatus.Conflicted },
    };

    [Theory]
    [MemberData(nameof(StatusCases))]
    public void ComputeGitStatus_MapsTreeAndTrackingState(bool clean, string? upstream, int ahead, int behind, int conflicts, GitStatus expected)
    {
        var tree = new WorkingTreeStatus(clean, 0, clean ? 0 : 1, 0, conflicts);
        var tracking = new TrackingStatus(upstream, ahead, behind);

        Assert.Equal(expected, TxtInstanceGitService.ComputeGitStatus(tree, tracking));
    }
}
