using PublicTxt.CLI;
using PublicTxt.Core.Instances;
using PublicTxt.Git;

namespace CliTests;

internal static class Cli
{
    public sealed record Result(int ExitCode, string Out, string Err);

    /// <summary>Runs the CLI in-process with an empty environment, so only explicit options and git config apply.</summary>
    public static Result Run(params string[] args) => Run(null, args);

    public static Result Run(IReadOnlyDictionary<string, string?>? env, params string[] args)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var app = new CliApp(output, error, key => env is not null && env.TryGetValue(key, out var v) ? v : null);
        var code = app.Run(args);
        return new Result(code, output.ToString(), error.ToString());
    }

    public static string Fixture
    {
        get
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir is not null)
            {
                var candidate = Path.Combine(dir.FullName, "tests", "fixtures", "sample-instance");
                if (Directory.Exists(candidate)) return candidate;
                dir = dir.Parent;
            }
            throw new DirectoryNotFoundException("tests/fixtures/sample-instance not found");
        }
    }

    public static string TempPath(string prefix) =>
        Path.Combine(Path.GetTempPath(), $"publictxt-cli-{prefix}-{Guid.NewGuid():N}");

    public static void Delete(string path)
    {
        if (!Directory.Exists(path)) return;
        foreach (var f in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
            File.SetAttributes(f, FileAttributes.Normal);
        Directory.Delete(path, recursive: true);
    }
}

public class ContentCommandTests
{
    [Fact]
    public void Help_ListsCommands()
    {
        var r = Cli.Run("--help");

        Assert.Equal(CliApp.ExitOk, r.ExitCode);
        foreach (var cmd in new[] { "init", "clone", "status", "list", "show", "links", "commit", "sync", "publish" })
            Assert.Contains(cmd, r.Out);
    }

    [Fact]
    public void List_OnFixture_PrintsAllItems()
    {
        var r = Cli.Run("list", "--path", Cli.Fixture);

        Assert.Equal(CliApp.ExitOk, r.ExitCode);
        Assert.Contains("wiki/index.md", r.Out);
        Assert.Contains("Home", r.Out);
        Assert.Contains("blog/2024/01/15/20240115.md", r.Out);
        Assert.Contains("2024-01-15", r.Out);
        Assert.DoesNotContain("README.md", r.Out);
    }

    [Fact]
    public void List_FiltersByTypeAndTag()
    {
        var byType = Cli.Run("list", "-C", Cli.Fixture, "--type", "notes");
        Assert.Contains("notes/todo.md", byType.Out);
        Assert.DoesNotContain("wiki/", byType.Out);

        var byTag = Cli.Run("list", "-C", Cli.Fixture, "--tag", "recipes");
        Assert.Contains("wiki/recipes/sourdough.md", byTag.Out);
        Assert.Contains("wiki/recipes/no-ext-page.md", byTag.Out);
        Assert.DoesNotContain("wiki/index.md", byTag.Out);
    }

    [Fact]
    public void Show_PrintsMetadataLinksAndBacklinks()
    {
        var r = Cli.Run("show", "wiki/index.md", "--path", Cli.Fixture);

        Assert.Equal(CliApp.ExitOk, r.ExitCode);
        Assert.Contains("title", r.Out);
        Assert.Contains("Home", r.Out);
        Assert.Contains("front matter:", r.Out);
        Assert.Contains("tags: [home]", r.Out);
        Assert.Contains("BROKEN missing-page.md", r.Out);
        Assert.Contains("recipes/sourdough.md -> wiki/recipes/sourdough.md", r.Out);
        Assert.Contains("https://example.com/", r.Out);
        Assert.Contains("backlinks:", r.Out);
        Assert.Contains("wiki/recipes/sourdough.md", r.Out);
        Assert.DoesNotContain("# Welcome", r.Out);
    }

    [Fact]
    public void Show_WithBody_PrintsMarkdown()
    {
        var r = Cli.Run("show", "notes/todo.md", "--body", "-C", Cli.Fixture);

        Assert.Contains("# Todo", r.Out);
        Assert.Contains("bake bread", r.Out);
    }

    [Fact]
    public void Show_MissingFile_FailsWithError()
    {
        var r = Cli.Run("show", "wiki/nope.md", "-C", Cli.Fixture);

        Assert.Equal(CliApp.ExitError, r.ExitCode);
        Assert.Contains("error: No Markdown item at: wiki/nope.md", r.Err);
    }

    [Fact]
    public void Links_ReportsBrokenLinks_AndFails()
    {
        var r = Cli.Run("links", "-C", Cli.Fixture);

        Assert.Equal(CliApp.ExitError, r.ExitCode);
        Assert.Contains("wiki/missing-page.md", r.Out);
        Assert.Contains("(outside instance)", r.Out);
        Assert.Contains("2 broken of", r.Out);
    }

    [Fact]
    public void Links_All_ListsEverything_AndSucceeds()
    {
        var r = Cli.Run("links", "--all", "-C", Cli.Fixture);

        Assert.Equal(CliApp.ExitOk, r.ExitCode);
        Assert.Contains("ok", r.Out);
        Assert.Contains("broken", r.Out);
        Assert.Contains("wiki/recipes/no-ext-page.md", r.Out);
    }

    [Fact]
    public void Status_OnFixture_ReportsNoRepoAndContentCounts()
    {
        var r = Cli.Run("status", "-C", Cli.Fixture);

        Assert.Equal(CliApp.ExitOk, r.ExitCode);
        Assert.Contains("not a repository", r.Out);
        Assert.Contains("blog 2", r.Out);
        Assert.Contains("wiki 3", r.Out);
        Assert.Contains("broken links  2", r.Out);
    }

    [Fact]
    public void Commands_OnMissingDirectory_Fail()
    {
        var r = Cli.Run("list", "-C", Cli.TempPath("missing"));

        Assert.Equal(CliApp.ExitError, r.ExitCode);
        Assert.Contains("does not exist", r.Err);
    }

    [Theory]
    [InlineData("Alice Smith <alice@example.com>", "Alice Smith", "alice@example.com")]
    [InlineData("  Bob <b@c.d>  ", "Bob", "b@c.d")]
    public void ParseAuthor_AcceptsNameEmailForm(string input, string name, string email)
    {
        Assert.Equal(new GitIdentity(name, email), CliApp.ParseAuthor(input));
    }

    [Theory]
    [InlineData("alice@example.com")]
    [InlineData("<alice@example.com>")]
    [InlineData("Alice <>")]
    public void ParseAuthor_RejectsOtherForms(string input)
    {
        Assert.ThrowsAny<Exception>(() => CliApp.ParseAuthor(input));
    }

    [Theory]
    [InlineData("https://github.com/org/repo.git", "repo")]
    [InlineData("https://github.com/org/repo", "repo")]
    [InlineData(@"C:\src\bare.git\", "bare")]
    [InlineData("/", "publictxt")]
    public void DeriveDirectoryName_FromUrl(string url, string expected)
    {
        Assert.Equal(expected, CliApp.DeriveDirectoryName(url));
    }
}

public class GitCommandTests : IDisposable
{
    private const string Author = "Test User <test@example.com>";
    private readonly string _bare = Cli.TempPath("bare");
    private readonly string _a = Cli.TempPath("a");
    private readonly string _b = Cli.TempPath("b");

    public GitCommandTests()
    {
        Directory.CreateDirectory(_bare);
        LibGit2Sharp.Repository.Init(_bare, isBare: true);
    }

    public void Dispose()
    {
        foreach (var p in new[] { _bare, _a, _b }) Cli.Delete(p);
    }

    [Fact]
    public void Init_CreatesInstanceSkeletonAndInitialCommit()
    {
        var r = Cli.Run("init", _a, "--author", Author);

        Assert.Equal(CliApp.ExitOk, r.ExitCode);
        Assert.Contains("Initialised PublicTxt instance", r.Out);
        Assert.True(InstanceLayout.IsInstance(_a));
        var repo = new PrimaryRepository(_a);
        Assert.True(repo.IsInitialized);
        Assert.Equal("Initialise PublicTxt instance", repo.LatestCommit!.Message);
        Assert.True(repo.GetStatus().IsClean);

        var again = Cli.Run("init", _a, "--author", Author);
        Assert.Contains("already exists", again.Out);
    }

    [Fact]
    public void Init_NoCommit_LeavesFilesUnstaged()
    {
        var r = Cli.Run("init", _a, "--no-commit");

        Assert.Equal(CliApp.ExitOk, r.ExitCode);
        Assert.Null(new PrimaryRepository(_a).LatestCommit);
    }

    [Fact]
    public void Status_AfterInit_ShowsCleanRepo()
    {
        Cli.Run("init", _a, "--author", Author);

        var r = Cli.Run("status", "-C", _a);

        Assert.Equal(CliApp.ExitOk, r.ExitCode);
        Assert.Contains("working tree  clean", r.Out);
        Assert.Contains("Initialise PublicTxt instance", r.Out);
        Assert.Contains("broken links  0", r.Out);
    }

    [Fact]
    public void Commit_StagesAndCommits_ThenReportsNothingToCommit()
    {
        Cli.Run("init", _a, "--author", Author);
        File.WriteAllText(Path.Combine(_a, "wiki", "page.md"), "# Page\n");

        var r = Cli.Run("commit", "-C", _a, "-m", "add page", "--author", Author);
        Assert.Equal(CliApp.ExitOk, r.ExitCode);
        Assert.Contains("add page", r.Out);

        var again = Cli.Run("commit", "-C", _a, "-m", "noop", "--author", Author);
        Assert.Contains("Nothing to commit", again.Out);
    }

    [Fact]
    public void Commit_WithoutMessage_Fails()
    {
        Cli.Run("init", _a, "--author", Author);

        var r = Cli.Run("commit", "-C", _a, "--author", Author);

        Assert.Equal(CliApp.ExitError, r.ExitCode);
        Assert.Contains("commit message is required", r.Err);
    }

    [Fact]
    public void Sync_Clone_And_List_RoundTrip()
    {
        Cli.Run("init", _a, "--remote", _bare, "--author", Author);
        File.WriteAllText(Path.Combine(_a, "wiki", "hello.md"), "# Hello\n\n#greeting\n");

        var sync = Cli.Run("sync", "-C", _a, "-m", "add hello", "--author", Author);
        Assert.Equal(CliApp.ExitOk, sync.ExitCode);
        Assert.Contains("add hello", sync.Out);
        Assert.Contains("pushed     yes", sync.Out);
        Assert.Contains("Synced", sync.Out);

        var clone = Cli.Run("clone", _bare, _b);
        Assert.Equal(CliApp.ExitOk, clone.ExitCode);
        Assert.Contains("Cloned", clone.Out);
        Assert.DoesNotContain("warning", clone.Out);

        var list = Cli.Run("list", "-C", _b, "--tag", "greeting");
        Assert.Contains("wiki/hello.md", list.Out);
        Assert.Contains("Hello", list.Out);
    }

    [Fact]
    public void Sync_WithoutMessageAndDirtyTree_Fails()
    {
        Cli.Run("init", _a, "--remote", _bare, "--author", Author);
        File.WriteAllText(Path.Combine(_a, "notes", "n.md"), "# N\n");

        var r = Cli.Run("sync", "-C", _a, "--author", Author);

        Assert.Equal(CliApp.ExitError, r.ExitCode);
        Assert.Contains("uncommitted changes", r.Err);
    }

    [Fact]
    public void Sync_ReportsConflicts_WithExitCode2()
    {
        Cli.Run("init", _a, "--remote", _bare, "--author", Author);
        File.WriteAllText(Path.Combine(_a, "wiki", "shared.md"), "from A\n");
        Cli.Run("sync", "-C", _a, "-m", "a", "--author", Author);
        Cli.Run("clone", _bare, _b);

        File.WriteAllText(Path.Combine(_a, "wiki", "shared.md"), "A again\n");
        Cli.Run("sync", "-C", _a, "-m", "a2", "--author", Author);
        File.WriteAllText(Path.Combine(_b, "wiki", "shared.md"), "B edit\n");
        var r = Cli.Run("sync", "-C", _b, "-m", "b", "--author", Author);

        Assert.Equal(CliApp.ExitConflicts, r.ExitCode);
        Assert.Contains("Conflicts", r.Out);
        Assert.Contains("wiki/shared.md", r.Out);
        Assert.Contains("Conflicted", r.Out);
    }

    [Fact]
    public void Publish_PushesToPagesBranch()
    {
        Cli.Run("init", _a, "--remote", _bare, "--author", Author);
        File.WriteAllText(Path.Combine(_a, "wiki", "site.md"), "# Site\n");
        Cli.Run("sync", "-C", _a, "-m", "site", "--author", Author);

        var r = Cli.Run("publish", "-C", _a);

        Assert.Equal(CliApp.ExitOk, r.ExitCode);
        Assert.Contains("to origin/gh-pages", r.Out);
        var ext = new ExternalRepository(_b);
        ext.CloneOrUpdate(_bare);
        Assert.Contains("origin/gh-pages", ext.GetRemoteBranches());
        Assert.Equal("# Site\n", ext.ReadFileAt("origin/gh-pages", "wiki/site.md"));
    }

    [Fact]
    public void Clone_IntoNonEmptyDirectory_Fails()
    {
        Directory.CreateDirectory(_b);
        File.WriteAllText(Path.Combine(_b, "x.txt"), "x");

        var r = Cli.Run("clone", _bare, _b);

        Assert.Equal(CliApp.ExitError, r.ExitCode);
        Assert.Contains("not empty", r.Err);
    }

    [Fact]
    public void Author_FallsBackToEnvironment()
    {
        var env = new Dictionary<string, string?>
        {
            ["PUBLICTXT_AUTHOR_NAME"] = "Env User",
            ["PUBLICTXT_AUTHOR_EMAIL"] = "env@example.com"
        };

        var r = Cli.Run(env, "init", _a);

        Assert.Equal(CliApp.ExitOk, r.ExitCode);
        Assert.Equal("Env User", new PrimaryRepository(_a).LatestCommit!.Author.Name);
    }
}
