using PublicTxt.CLI;

namespace CliTests;

public class SubscriptionCommandTests : IDisposable
{
    private const string Author = "Test User <test@example.com>";
    private readonly string _aliceBare = Cli.TempPath("alice-bare");
    private readonly string _aliceWork = Cli.TempPath("alice-work");
    private readonly string _local = Cli.TempPath("local");

    public SubscriptionCommandTests()
    {
        Directory.CreateDirectory(_aliceBare);
        LibGit2Sharp.Repository.Init(_aliceBare, isBare: true);

        // Alice publishes an instance with a recipe, a draft and a post.
        Cli.Run("init", _aliceWork, "--remote", _aliceBare, "--author", Author);
        Directory.CreateDirectory(Path.Combine(_aliceWork, "wiki", "recipes"));
        File.WriteAllText(Path.Combine(_aliceWork, "wiki", "recipes", "bread.md"), "# Bread\n\n#recipes\n");
        File.WriteAllText(Path.Combine(_aliceWork, "wiki", "recipes", "cake.md"), "# Cake\n\n#recipes #draft\n");
        Directory.CreateDirectory(Path.Combine(_aliceWork, "blog", "2024", "05", "01"));
        File.WriteAllText(Path.Combine(_aliceWork, "blog", "2024", "05", "01", "20240501.md"), "# Alice post\n");
        Cli.Run("sync", "-C", _aliceWork, "-m", "content", "--author", Author);

        // Local instance with one page.
        Cli.Run("init", _local, "--author", Author);
        File.WriteAllText(Path.Combine(_local, "wiki", "mine.md"), "# Mine\n");
    }

    public void Dispose()
    {
        foreach (var p in new[] { _aliceBare, _aliceWork, _local }) Cli.Delete(p);
    }

    [Fact]
    public void Add_List_Update_Remove_RoundTrip()
    {
        var add = Cli.Run("subscriptions", "add", _aliceBare, "-C", _local, "--name", "alice", "--type", "wiki", "--exclude-tag", "draft");
        Assert.Equal(CliApp.ExitOk, add.ExitCode);
        Assert.Contains("Subscribed to 'alice'", add.Out);
        Assert.Contains("types=wiki -tags=draft", add.Out);
        Assert.Contains("items   1", add.Out);
        Assert.True(File.Exists(Path.Combine(_local, "settings", "subscriptions.json")));
        Assert.Contains(".publictxt/", File.ReadAllText(Path.Combine(_local, ".gitignore")));

        var list = Cli.Run("subs", "ls", "-C", _local);
        Assert.Equal(CliApp.ExitOk, list.ExitCode);
        Assert.Contains("alice", list.Out);
        Assert.Contains("types=wiki -tags=draft", list.Out);

        var update = Cli.Run("subscriptions", "update", "-C", _local);
        Assert.Equal(CliApp.ExitOk, update.ExitCode);
        Assert.Contains("up to date", update.Out);

        var remove = Cli.Run("subscriptions", "remove", "alice", "-C", _local);
        Assert.Equal(CliApp.ExitOk, remove.ExitCode);
        Assert.False(Directory.Exists(Path.Combine(_local, ".publictxt", "subscriptions", "alice")));
        Assert.Contains("(none)", Cli.Run("subscriptions", "list", "-C", _local).Out);

        var removeAgain = Cli.Run("subscriptions", "rm", "alice", "-C", _local);
        Assert.Equal(CliApp.ExitError, removeAgain.ExitCode);
    }

    [Fact]
    public void List_All_ShowsLocalAndSubscribedItems_WithSourceColumn()
    {
        Cli.Run("subscriptions", "add", _aliceBare, "-C", _local, "--name", "alice");

        var local = Cli.Run("list", "-C", _local);
        Assert.Contains("wiki/mine.md", local.Out);
        Assert.DoesNotContain("bread.md", local.Out);
        Assert.DoesNotContain("source", local.Out);

        var all = Cli.Run("list", "--all", "-C", _local);
        Assert.Equal(CliApp.ExitOk, all.ExitCode);
        Assert.Contains("source", all.Out);
        Assert.Contains("(local)", all.Out);
        Assert.Contains("alice", all.Out);
        Assert.Contains("wiki/recipes/bread.md", all.Out);
        Assert.Contains("wiki/recipes/cake.md", all.Out);
        Assert.Contains("blog/2024/05/01/20240501.md", all.Out);
    }

    [Fact]
    public void List_All_WithFiltersAndTimeline()
    {
        Cli.Run("subscriptions", "add", _aliceBare, "-C", _local, "--name", "alice");

        var tagged = Cli.Run("list", "-a", "--tag", "draft", "-C", _local);
        Assert.Contains("cake.md", tagged.Out);
        Assert.DoesNotContain("bread.md", tagged.Out);

        var searched = Cli.Run("list", "-a", "--search", "alice post", "-C", _local);
        Assert.Contains("20240501.md", searched.Out);
        Assert.DoesNotContain("bread.md", searched.Out);

        var timeline = Cli.Run("list", "-a", "--timeline", "-C", _local);
        var lines = timeline.Out.Split('\n').Where(l => l.Contains(".md")).ToList();
        Assert.StartsWith("alice", lines[0].TrimStart());
        Assert.Contains("2024-05-01", lines[0]);
    }

    [Fact]
    public void Update_PicksUpNewRemoteContent()
    {
        Cli.Run("subscriptions", "add", _aliceBare, "-C", _local, "--name", "alice", "--include", "wiki/**");
        File.WriteAllText(Path.Combine(_aliceWork, "wiki", "later.md"), "# Later\n");
        Cli.Run("sync", "-C", _aliceWork, "-m", "later", "--author", Author);

        Assert.DoesNotContain("later.md", Cli.Run("list", "-a", "-C", _local).Out);
        var update = Cli.Run("subscriptions", "update", "alice", "-C", _local);

        Assert.Equal(CliApp.ExitOk, update.ExitCode);
        Assert.Contains("updated", update.Out);
        Assert.Contains("wiki/later.md", Cli.Run("list", "-a", "-C", _local).Out);
    }

    [Fact]
    public void Update_ReportsFailure_WithExitCode1()
    {
        Cli.Run("subscriptions", "add", _aliceBare, "-C", _local, "--name", "alice");
        Cli.Delete(_aliceBare);

        var update = Cli.Run("subscriptions", "update", "-C", _local);

        Assert.Equal(CliApp.ExitError, update.ExitCode);
        Assert.Contains("failed", update.Out);
    }

    [Fact]
    public void Status_ShowsSubscriptionSummary()
    {
        Cli.Run("subscriptions", "add", _aliceBare, "-C", _local, "--name", "alice", "--type", "blog");

        var status = Cli.Run("status", "-C", _local);

        Assert.Contains("subscriptions  alice 1", status.Out);
    }

    [Fact]
    public void Add_OutsideInstance_Fails()
    {
        var dir = Cli.TempPath("plain");
        Directory.CreateDirectory(dir);
        try
        {
            var r = Cli.Run("subscriptions", "add", _aliceBare, "-C", dir);
            Assert.Equal(CliApp.ExitError, r.ExitCode);
            Assert.Contains("Not a PublicTxt instance", r.Err);
        }
        finally { Cli.Delete(dir); }
    }

    [Fact]
    public void Add_BadRemote_Fails_AndLeavesNoSubscription()
    {
        var r = Cli.Run("subscriptions", "add", Cli.TempPath("missing"), "-C", _local, "--name", "ghost");

        Assert.Equal(CliApp.ExitError, r.ExitCode);
        Assert.Contains("(none)", Cli.Run("subscriptions", "list", "-C", _local).Out);
    }

    [Fact]
    public void List_All_WarnsAboutUnfetchedSubscription_ButStillListsLocal()
    {
        Cli.Run("subscriptions", "add", _aliceBare, "-C", _local, "--name", "alice");
        Cli.Delete(Path.Combine(_local, ".publictxt", "subscriptions", "alice"));

        var r = Cli.Run("list", "-a", "-C", _local);

        Assert.Equal(CliApp.ExitOk, r.ExitCode);
        Assert.Contains("warning: subscription 'alice' skipped", r.Err);
        Assert.Contains("wiki/mine.md", r.Out);
    }
}
