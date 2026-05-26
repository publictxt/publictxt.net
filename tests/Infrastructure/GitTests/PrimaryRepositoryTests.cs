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
        if (Directory.Exists(_repoPath))
            Directory.Delete(_repoPath, recursive: true);
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
        var repo = new PrimaryRepository(_repoPath);
        repo.Init();

        File.WriteAllText(Path.Combine(_repoPath, "a.txt"), "a");
        repo.StageAll();
        repo.Commit("init", new GitIdentity("Bot", "bot@example.com"));

        // Create a second branch via LibGit2Sharp directly so we can check it out
        using var raw = new LibGit2Sharp.Repository(_repoPath);
        raw.Branches.Add("feature", raw.Head.Tip);

        repo.Checkout("feature");
        Assert.Equal("feature", repo.CurrentBranch);
    }
}
