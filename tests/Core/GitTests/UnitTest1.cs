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

        // Need at least one commit before creating branches
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

public class ExternalRepositoryTests : IDisposable
{
    private readonly string _sourcePath;
    private readonly string _externalPath;

    public ExternalRepositoryTests()
    {
        var id = Guid.NewGuid().ToString("N");
        _sourcePath = Path.Combine(Path.GetTempPath(), $"publictxt-test-source-{id}");
        _externalPath = Path.Combine(Path.GetTempPath(), $"publictxt-test-external-{id}");
    }

    public void Dispose()
    {
        foreach (var path in new[] { _sourcePath, _externalPath })
        {
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }
    }

    private void CreateSourceRepo()
    {
        var primary = new PrimaryRepository(_sourcePath);
        primary.Init();
        Directory.CreateDirectory(Path.Combine(_sourcePath, "blog"));
        File.WriteAllText(Path.Combine(_sourcePath, "index.txt"), "hello publictxt");
        File.WriteAllText(Path.Combine(_sourcePath, "blog", "post1.md"), "# Post 1");
        primary.StageAll();
        primary.Commit("initial", new GitIdentity("Origin", "origin@example.com"));
    }

    [Fact]
    public void IsInitialized_ReturnsFalse_BeforeClone()
    {
        var ext = new ExternalRepository(_externalPath, "https://example.com/repo.git");
        Assert.False(ext.IsInitialized);
    }

    [Fact]
    public void CloneOrUpdate_ClonesLocalRepo_AndInitializes()
    {
        CreateSourceRepo();
        var ext = new ExternalRepository(_externalPath);
        ext.CloneOrUpdate(_sourcePath);

        Assert.True(ext.IsInitialized);
        Assert.Equal(_sourcePath, ext.RemoteUrl);
    }

    [Fact]
    public void CurrentBranch_IsSet_AfterClone()
    {
        CreateSourceRepo();
        var ext = new ExternalRepository(_externalPath);
        ext.CloneOrUpdate(_sourcePath);

        Assert.NotNull(ext.CurrentBranch);
    }

    [Fact]
    public void LatestCommit_IsAvailable_AfterClone()
    {
        CreateSourceRepo();
        var ext = new ExternalRepository(_externalPath);
        ext.CloneOrUpdate(_sourcePath);

        Assert.NotNull(ext.LatestCommit);
        Assert.Equal("initial", ext.LatestCommit!.Message);
    }

    [Fact]
    public void ReadFile_ReturnsContent_ForExistingFile()
    {
        CreateSourceRepo();
        var ext = new ExternalRepository(_externalPath);
        ext.CloneOrUpdate(_sourcePath);

        var content = ext.ReadFile("index.txt");
        Assert.Equal("hello publictxt", content);
    }

    [Fact]
    public void ReadFile_ThrowsFileNotFoundException_ForMissingFile()
    {
        CreateSourceRepo();
        var ext = new ExternalRepository(_externalPath);
        ext.CloneOrUpdate(_sourcePath);

        Assert.Throws<FileNotFoundException>(() => ext.ReadFile("does-not-exist.txt"));
    }

    [Fact]
    public void ListFiles_ReturnsAllTrackedFiles()
    {
        CreateSourceRepo();
        var ext = new ExternalRepository(_externalPath);
        ext.CloneOrUpdate(_sourcePath);

        var files = ext.ListFiles().ToList();
        Assert.Contains("index.txt", files);
        Assert.Contains("blog/post1.md", files);
    }

    [Fact]
    public void ListFiles_WithGlobPattern_FiltersCorrectly()
    {
        CreateSourceRepo();
        var ext = new ExternalRepository(_externalPath);
        ext.CloneOrUpdate(_sourcePath);

        var markdownFiles = ext.ListFiles("**/*.md").ToList();
        Assert.All(markdownFiles, f => Assert.EndsWith(".md", f));
    }

    [Fact]
    public void GetStatus_IsClean_AfterFreshClone()
    {
        CreateSourceRepo();
        var ext = new ExternalRepository(_externalPath);
        ext.CloneOrUpdate(_sourcePath);

        var status = ext.GetStatus();
        Assert.True(status.IsClean);
    }

    [Fact]
    public void CloneOrUpdate_CalledTwice_DoesNotThrow()
    {
        CreateSourceRepo();
        var ext = new ExternalRepository(_externalPath);
        ext.CloneOrUpdate(_sourcePath);

        // second call should fetch + fast-forward without error
        var ex = Record.Exception(() => ext.CloneOrUpdate(_sourcePath));
        Assert.Null(ex);
    }
}