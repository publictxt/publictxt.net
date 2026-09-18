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
            GitTestHelpers.DeleteDirectory(path);
        }
    }

    private static readonly GitIdentity OriginIdentity = new("Origin", "origin@example.com");

    private PrimaryRepository CreateSourceRepo()
    {
        var primary = new PrimaryRepository(_sourcePath);
        primary.Init();
        Directory.CreateDirectory(Path.Combine(_sourcePath, "blog"));
        File.WriteAllText(Path.Combine(_sourcePath, "index.txt"), "hello publictxt");
        File.WriteAllText(Path.Combine(_sourcePath, "README.md"), "# Root readme");
        File.WriteAllText(Path.Combine(_sourcePath, "blog", "post1.md"), "# Post 1");
        primary.StageAll();
        primary.Commit("initial", OriginIdentity);
        return primary;
    }

    /// <summary>Adds a commit on a new topic branch in the source repo, then returns to the original branch.</summary>
    private void AddTopicBranchToSource(string branchName, string filePath, string content)
    {
        using var raw = new LibGit2Sharp.Repository(_sourcePath);
        var original = raw.Head.FriendlyName;
        var topic = raw.Branches.Add(branchName, raw.Head.Tip);
        LibGit2Sharp.Commands.Checkout(raw, topic);

        var fullPath = Path.Combine(_sourcePath, filePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, content);
        LibGit2Sharp.Commands.Stage(raw, filePath);
        var sig = new LibGit2Sharp.Signature(OriginIdentity.Name, OriginIdentity.Email, DateTimeOffset.UtcNow);
        raw.Commit($"topic: {branchName}", sig, sig, new LibGit2Sharp.CommitOptions());

        LibGit2Sharp.Commands.Checkout(raw, raw.Branches[original]);
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

    [Fact]
    public void FastForward_ThrowsInvalidOperationException_WhenRemoteDoesNotExist()
    {
        CreateSourceRepo();
        var ext = new ExternalRepository(_externalPath);
        ext.CloneOrUpdate(_sourcePath);

        var ex = Assert.Throws<InvalidOperationException>(() => ext.FastForward("missing"));
        Assert.Contains("Remote 'missing' not found", ex.Message);
    }

    // ── glob ─────────────────────────────────────────────────────────────────

    [Fact]
    public void ListFiles_RecursiveGlob_IncludesRootLevelFiles()
    {
        CreateSourceRepo();
        var ext = new ExternalRepository(_externalPath);
        ext.CloneOrUpdate(_sourcePath);

        var markdownFiles = ext.ListFiles("**/*.md").ToList();

        Assert.Contains("README.md", markdownFiles);
        Assert.Contains("blog/post1.md", markdownFiles);
        Assert.DoesNotContain("index.txt", markdownFiles);
    }

    [Fact]
    public void ListFiles_DirectoryGlob_ReturnsOnlyThatDirectory()
    {
        CreateSourceRepo();
        var ext = new ExternalRepository(_externalPath);
        ext.CloneOrUpdate(_sourcePath);

        var blogFiles = ext.ListFiles("blog/**").ToList();

        Assert.Equal(new[] { "blog/post1.md" }, blogFiles);
    }

    // ── update / fast-forward ────────────────────────────────────────────────

    [Fact]
    public void CloneOrUpdate_SecondCall_FastForwardsNewCommits()
    {
        var source = CreateSourceRepo();
        var ext = new ExternalRepository(_externalPath);
        ext.CloneOrUpdate(_sourcePath);
        var firstSha = ext.LatestCommit!.Sha;

        File.WriteAllText(Path.Combine(_sourcePath, "wiki.md"), "# Wiki");
        source.StageAll();
        var second = source.Commit("add wiki", OriginIdentity);

        ext.CloneOrUpdate(_sourcePath);

        Assert.NotEqual(firstSha, ext.LatestCommit!.Sha);
        Assert.Equal(second.Sha, ext.LatestCommit.Sha);
        Assert.Equal("# Wiki", ext.ReadFile("wiki.md"));
    }

    [Fact]
    public void GetRemoteBranches_ListsTopicBranches()
    {
        CreateSourceRepo();
        AddTopicBranchToSource("topic/recipes", "wiki/recipes.md", "# Recipes");
        var ext = new ExternalRepository(_externalPath);
        ext.CloneOrUpdate(_sourcePath);

        var branches = ext.GetRemoteBranches().ToList();

        Assert.Contains("origin/topic/recipes", branches);
    }

    // ── UpdateToBranch ───────────────────────────────────────────────────────

    [Fact]
    public void UpdateToBranch_ClonesDefaultBranch_WhenBranchNull()
    {
        var source = CreateSourceRepo();
        var ext = new ExternalRepository(_externalPath);

        var sha = ext.UpdateToBranch(_sourcePath, null);

        Assert.Equal(source.LatestCommit!.Sha, sha);
        Assert.Equal(source.CurrentBranch, ext.CurrentBranch);
        Assert.Equal("hello publictxt", ext.ReadFile("index.txt"));
    }

    [Fact]
    public void UpdateToBranch_ChecksOutTopicBranch_AndSwitchesBackAndForth()
    {
        CreateSourceRepo();
        AddTopicBranchToSource("topic/recipes", "wiki/recipes.md", "# Recipes");
        var ext = new ExternalRepository(_externalPath);

        ext.UpdateToBranch(_sourcePath, "topic/recipes");
        Assert.Equal("topic/recipes", ext.CurrentBranch);
        Assert.True(File.Exists(Path.Combine(_externalPath, "wiki", "recipes.md")));

        ext.UpdateToBranch(_sourcePath, null);
        Assert.NotEqual("topic/recipes", ext.CurrentBranch);
        Assert.False(File.Exists(Path.Combine(_externalPath, "wiki", "recipes.md")));
        Assert.True(ext.GetStatus().IsClean);
    }

    [Fact]
    public void UpdateToBranch_PicksUpNewCommits_AndSurvivesForcePush()
    {
        var source = CreateSourceRepo();
        var ext = new ExternalRepository(_externalPath);
        ext.UpdateToBranch(_sourcePath, null);

        File.WriteAllText(Path.Combine(_sourcePath, "new.md"), "# New");
        source.StageAll();
        var second = source.Commit("second", OriginIdentity);
        Assert.Equal(second.Sha, ext.UpdateToBranch(_sourcePath, null));
        Assert.Equal("# New", ext.ReadFile("new.md"));

        // Rewrite history on the source: reset to the first commit and add a different one.
        using (var raw = new LibGit2Sharp.Repository(_sourcePath))
        {
            raw.Reset(LibGit2Sharp.ResetMode.Hard, raw.Head.Tip.Parents.First());
        }
        File.WriteAllText(Path.Combine(_sourcePath, "other.md"), "# Other");
        source.StageAll();
        var rewritten = source.Commit("rewritten", OriginIdentity);

        Assert.Equal(rewritten.Sha, ext.UpdateToBranch(_sourcePath, null));
        Assert.False(File.Exists(Path.Combine(_externalPath, "new.md")));
        Assert.Equal("# Other", ext.ReadFile("other.md"));
    }

    [Fact]
    public void UpdateToBranch_Throws_ForMissingBranch()
    {
        CreateSourceRepo();
        var ext = new ExternalRepository(_externalPath);

        var ex = Assert.Throws<InvalidOperationException>(() => ext.UpdateToBranch(_sourcePath, "nope"));
        Assert.Contains("nope", ex.Message);
    }

    // ── reading at a reference ───────────────────────────────────────────────

    [Fact]
    public void ReadFileAt_ReadsFromBranchWithoutCheckout()
    {
        CreateSourceRepo();
        AddTopicBranchToSource("topic/recipes", "wiki/recipes.md", "# Recipes");
        var ext = new ExternalRepository(_externalPath);
        ext.CloneOrUpdate(_sourcePath);
        var branchBefore = ext.CurrentBranch;

        var content = ext.ReadFileAt("origin/topic/recipes", "wiki/recipes.md");

        Assert.Equal("# Recipes", content);
        Assert.Equal(branchBefore, ext.CurrentBranch);
        Assert.False(File.Exists(Path.Combine(_externalPath, "wiki", "recipes.md")));
    }

    [Fact]
    public void ReadFileAt_AcceptsBackslashPaths()
    {
        CreateSourceRepo();
        var ext = new ExternalRepository(_externalPath);
        ext.CloneOrUpdate(_sourcePath);

        var content = ext.ReadFileAt("HEAD", @"blog\post1.md");

        Assert.Equal("# Post 1", content);
    }

    [Fact]
    public void ReadFileAt_ThrowsFileNotFound_ForMissingFile()
    {
        CreateSourceRepo();
        var ext = new ExternalRepository(_externalPath);
        ext.CloneOrUpdate(_sourcePath);

        Assert.Throws<FileNotFoundException>(() => ext.ReadFileAt("HEAD", "nope.md"));
    }

    [Fact]
    public void ReadFileAt_ThrowsInvalidOperation_ForUnknownReference()
    {
        CreateSourceRepo();
        var ext = new ExternalRepository(_externalPath);
        ext.CloneOrUpdate(_sourcePath);

        Assert.Throws<InvalidOperationException>(() => ext.ReadFileAt("origin/does-not-exist", "index.txt"));
    }

    [Fact]
    public void ReadFileAt_ThrowsUnauthorizedAccess_ForPathTraversal()
    {
        CreateSourceRepo();
        var ext = new ExternalRepository(_externalPath);
        ext.CloneOrUpdate(_sourcePath);

        Assert.Throws<UnauthorizedAccessException>(() => ext.ReadFileAt("HEAD", "../outside.txt"));
    }

    [Fact]
    public void ListFilesAt_ListsTopicBranchFiles_WithGlob()
    {
        CreateSourceRepo();
        AddTopicBranchToSource("topic/recipes", "wiki/recipes.md", "# Recipes");
        var ext = new ExternalRepository(_externalPath);
        ext.CloneOrUpdate(_sourcePath);

        var topicFiles = ext.ListFilesAt("origin/topic/recipes", "**/*.md").ToList();
        var headFiles = ext.ListFilesAt("HEAD", "**/*.md").ToList();

        Assert.Contains("wiki/recipes.md", topicFiles);
        Assert.Contains("README.md", topicFiles);
        Assert.DoesNotContain("wiki/recipes.md", headFiles);
    }
}
