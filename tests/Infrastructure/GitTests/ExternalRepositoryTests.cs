using PublicTxt.Git;

namespace GitTests;

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
    public void ReadFile_ReturnsContent_ForNestedPathWithinRepository()
    {
        CreateSourceRepo();
        var ext = new ExternalRepository(_externalPath);
        ext.CloneOrUpdate(_sourcePath);

        var content = ext.ReadFile(Path.Combine("blog", "post1.md"));
        Assert.Equal("# Post 1", content);
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
    public void ReadFile_ThrowsUnauthorizedAccessException_ForPathTraversal()
    {
        CreateSourceRepo();
        var ext = new ExternalRepository(_externalPath);
        ext.CloneOrUpdate(_sourcePath);

        var parentDir = Directory.GetParent(_externalPath)!.FullName;
        var outsideFileName = $"outside-{Guid.NewGuid():N}.txt";
        var outsideFilePath = Path.Combine(parentDir, outsideFileName);
        File.WriteAllText(outsideFilePath, "secret");

        try
        {
            var traversalPath = Path.Combine("..", outsideFileName);
            Assert.Throws<UnauthorizedAccessException>(() => ext.ReadFile(traversalPath));
        }
        finally
        {
            if (File.Exists(outsideFilePath))
                File.Delete(outsideFilePath);
        }
    }

    [Fact]
    public void ReadFile_ThrowsUnauthorizedAccessException_ForAbsolutePath()
    {
        CreateSourceRepo();
        var ext = new ExternalRepository(_externalPath);
        ext.CloneOrUpdate(_sourcePath);

        var outsideFilePath = Path.Combine(Path.GetTempPath(), $"outside-abs-{Guid.NewGuid():N}.txt");
        File.WriteAllText(outsideFilePath, "secret");

        try
        {
            Assert.Throws<UnauthorizedAccessException>(() => ext.ReadFile(outsideFilePath));
        }
        finally
        {
            if (File.Exists(outsideFilePath))
                File.Delete(outsideFilePath);
        }
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

        var ex = Record.Exception(() => ext.CloneOrUpdate(_sourcePath));
        Assert.Null(ex);
    }
}
