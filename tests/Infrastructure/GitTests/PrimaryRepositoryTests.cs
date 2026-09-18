using PublicTxt.Git;

namespace GitTests;

public class PrimaryRepositoryTests : IDisposable
{
    private readonly string _repoPath;

    public PrimaryRepositoryTests()
    {
        _repoPath = Path.Combine(Path.GetTempPath(), $"publictxt-test-primary-{Guid.NewGuid():N}");
    }

    public void Dispose()
    {
        GitTestHelpers.DeleteDirectory(_repoPath);
    }

    [Fact]
    public void IsInitialized_ReturnsFalse_BeforeInit()
    {
        var repo = new PrimaryRepository(_repoPath);
        Assert.False(repo.IsInitialized);
    }

    [Fact]
    public void Init_CreatesValidRepository()
    {
        var repo = new PrimaryRepository(_repoPath);
        repo.Init();
        Assert.True(repo.IsInitialized);
    }

    [Fact]
    public void CurrentBranch_IsNotNull_AfterInit()
    {
        var repo = new PrimaryRepository(_repoPath);
        repo.Init();
        Assert.NotNull(repo.CurrentBranch);
    }

    [Fact]
    public void LatestCommit_IsNull_OnEmptyRepository()
    {
        var repo = new PrimaryRepository(_repoPath);
        repo.Init();
        Assert.Null(repo.LatestCommit);
    }

    [Fact]
    public void Commit_ReturnsCommitInfo_AfterStagingFile()
    {
        var repo = new PrimaryRepository(_repoPath);
        repo.Init();

        File.WriteAllText(Path.Combine(_repoPath, "readme.txt"), "hello");
        repo.Stage("readme.txt");

        var author = new GitIdentity("Test User", "test@example.com");
        var commit = repo.Commit("initial commit", author);

        Assert.NotNull(commit);
        Assert.Equal("initial commit", commit.Message);
        Assert.Equal("Test User", commit.Author.Name);
        Assert.NotEmpty(commit.Sha);
    }

    [Fact]
    public void LatestCommit_IsReturnedAfterCommit()
    {
        var repo = new PrimaryRepository(_repoPath);
        repo.Init();

        File.WriteAllText(Path.Combine(_repoPath, "file.txt"), "content");
        repo.StageAll();
        repo.Commit("first", new GitIdentity("Alice", "alice@example.com"));

        Assert.NotNull(repo.LatestCommit);
        Assert.Equal("first", repo.LatestCommit!.Message);
    }

    [Fact]
    public void GetStatus_IsClean_OnEmptyRepository()
    {
        var repo = new PrimaryRepository(_repoPath);
        repo.Init();
        var status = repo.GetStatus();
        Assert.True(status.IsClean);
    }

    [Fact]
    public void GetStatus_ThrowsInvalidOperationException_BeforeInit()
    {
        var repo = new PrimaryRepository(_repoPath);
        Assert.Throws<InvalidOperationException>(() => repo.GetStatus());
    }

    [Fact]
    public void GetStatus_ReflectsUntracked_WhenFileAdded()
    {
        var repo = new PrimaryRepository(_repoPath);
        repo.Init();
        File.WriteAllText(Path.Combine(_repoPath, "new.txt"), "data");

        var status = repo.GetStatus();
        Assert.False(status.IsClean);
        Assert.True(status.UntrackedCount > 0);
    }

    [Fact]
    public void Checkout_SwitchesBranch()
    {
        var repo = InitWithCommit();
        repo.CreateBranch("feature");

        repo.Checkout("feature");

        Assert.Equal("feature", repo.CurrentBranch);
    }

    [Fact]
    public void Checkout_Throws_WhenBranchMissing()
    {
        var repo = InitWithCommit();
        Assert.Throws<InvalidOperationException>(() => repo.Checkout("nope"));
    }

    // ── branches ─────────────────────────────────────────────────────────────

    [Fact]
    public void CreateBranch_AddsLocalBranch_WithoutSwitching()
    {
        var repo = InitWithCommit();
        var before = repo.CurrentBranch;

        repo.CreateBranch("topic/one");

        Assert.Equal(before, repo.CurrentBranch);
        Assert.Contains("topic/one", repo.GetLocalBranches());
    }

    [Fact]
    public void CreateBranch_WithCheckout_SwitchesToIt()
    {
        var repo = InitWithCommit();

        repo.CreateBranch("topic/two", checkout: true);

        Assert.Equal("topic/two", repo.CurrentBranch);
    }

    [Fact]
    public void CreateBranch_Throws_WhenBranchExists()
    {
        var repo = InitWithCommit();
        repo.CreateBranch("dup");

        var ex = Assert.Throws<InvalidOperationException>(() => repo.CreateBranch("dup"));
        Assert.Contains("already exists", ex.Message);
    }

    [Fact]
    public void CreateBranch_Throws_WhenNoCommits()
    {
        var repo = new PrimaryRepository(_repoPath);
        repo.Init();

        Assert.Throws<InvalidOperationException>(() => repo.CreateBranch("early"));
    }

    // ── remotes ──────────────────────────────────────────────────────────────

    [Fact]
    public void AddRemote_ThenGetRemotes_ListsIt()
    {
        var repo = InitWithCommit();

        repo.AddRemote("origin", "https://example.com/repo.git");

        var remote = Assert.Single(repo.GetRemotes());
        Assert.Equal("origin", remote.Name);
        Assert.Equal("https://example.com/repo.git", remote.Url);
    }

    [Fact]
    public void AddRemote_Throws_WhenNameExists()
    {
        var repo = InitWithCommit();
        repo.AddRemote("origin", "https://example.com/a.git");

        Assert.Throws<InvalidOperationException>(() => repo.AddRemote("origin", "https://example.com/b.git"));
    }

    [Fact]
    public void RemoveRemote_RemovesIt()
    {
        var repo = InitWithCommit();
        repo.AddRemote("origin", "https://example.com/repo.git");

        repo.RemoveRemote("origin");

        Assert.Empty(repo.GetRemotes());
    }

    [Fact]
    public void RemoveRemote_Throws_WhenMissing()
    {
        var repo = InitWithCommit();
        Assert.Throws<InvalidOperationException>(() => repo.RemoveRemote("missing"));
    }

    [Fact]
    public void GetRemotes_IsEmpty_BeforeInit()
    {
        var repo = new PrimaryRepository(_repoPath);
        Assert.Empty(repo.GetRemotes());
    }

    // ── commit / tracking / fetch guards ─────────────────────────────────────

    [Fact]
    public void Commit_Throws_WhenNothingStaged()
    {
        var repo = InitWithCommit();

        var ex = Assert.Throws<InvalidOperationException>(
            () => repo.Commit("empty", new GitIdentity("Bot", "bot@example.com")));

        Assert.Contains("Nothing to commit", ex.Message);
    }

    [Fact]
    public void GetTrackingStatus_IsNone_ForLocalOnlyRepository()
    {
        var repo = InitWithCommit();

        var tracking = repo.GetTrackingStatus();

        Assert.False(tracking.IsTracking);
        Assert.Null(tracking.UpstreamBranch);
        Assert.True(tracking.IsUpToDate);
    }

    [Fact]
    public void GetTrackingStatus_Throws_BeforeInit()
    {
        var repo = new PrimaryRepository(_repoPath);
        Assert.Throws<InvalidOperationException>(() => repo.GetTrackingStatus());
    }

    [Fact]
    public void Fetch_Throws_BeforeInit()
    {
        var repo = new PrimaryRepository(_repoPath);
        Assert.Throws<InvalidOperationException>(() => repo.Fetch());
    }

    [Fact]
    public void Fetch_Throws_WhenRemoteMissing()
    {
        var repo = InitWithCommit();
        var ex = Assert.Throws<InvalidOperationException>(() => repo.Fetch("missing"));
        Assert.Contains("Remote 'missing' not found", ex.Message);
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private PrimaryRepository InitWithCommit()
    {
        var repo = new PrimaryRepository(_repoPath);
        repo.Init();
        File.WriteAllText(Path.Combine(_repoPath, "a.txt"), "a");
        repo.StageAll();
        repo.Commit("init", new GitIdentity("Bot", "bot@example.com"));
        return repo;
    }

    [Fact]
    public void Pull_ThrowsInvalidOperationException_WhenRemoteDoesNotExist()
    {
        var repo = new PrimaryRepository(_repoPath);
        repo.Init();
        File.WriteAllText(Path.Combine(_repoPath, "a.txt"), "a");
        repo.StageAll();
        repo.Commit("init", new GitIdentity("Bot", "bot@example.com"));

        var ex = Assert.Throws<InvalidOperationException>(
            () => repo.Pull(new GitIdentity("Bot", "bot@example.com"), remote: "missing"));

        Assert.Contains("Remote 'missing' not found", ex.Message);
    }
}
