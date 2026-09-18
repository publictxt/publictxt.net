using PublicTxt.Core.Content;
using PublicTxt.Core.Instances;
using PublicTxt.Core.Subscriptions;
using PublicTxt.Git;

namespace GitTests;

/// <summary>
/// Two source instances (alice, bob) published to bare remotes; one local instance subscribes to both
/// with different filters and aggregates.
/// </summary>
public class SubscriptionServiceTests : IDisposable
{
    private static readonly GitIdentity Author = new("Author", "author@example.com");

    private readonly string _aliceWork = GitTestHelpers.TempPath("alice-work");
    private readonly string _aliceBare = GitTestHelpers.TempPath("alice-bare");
    private readonly string _bobWork = GitTestHelpers.TempPath("bob-work");
    private readonly string _bobBare = GitTestHelpers.TempPath("bob-bare");
    private readonly string _local = GitTestHelpers.TempPath("local");

    private readonly SubscriptionService _service;

    public SubscriptionServiceTests()
    {
        // Alice: a wiki with recipes and a draft, plus a blog post.
        var alice = CreateInstance(_aliceWork, _aliceBare, new Dictionary<string, string>
        {
            ["wiki/recipes/bread.md"] = "# Bread\n\n#recipes #baking",
            ["wiki/recipes/cake.md"] = "# Cake\n\n#recipes #draft",
            ["wiki/about.md"] = "# About Alice",
            ["blog/2024/03/01/20240301.md"] = "# Alice post\n\n#blog"
        });
        // Alice also has a topic branch with an extra page.
        AddTopicBranch(alice, _aliceWork, "topic/soup", "wiki/recipes/soup.md", "# Soup\n\n#recipes");
        new PrimaryRepository(_aliceWork).PushTo("origin", "topic/soup", "topic/soup");

        // Bob: notes and a blog.
        CreateInstance(_bobWork, _bobBare, new Dictionary<string, string>
        {
            ["notes/idea.md"] = "# Idea\n\n#ideas",
            ["blog/2024/04/01/20240401.md"] = "# Bob post\n\n#blog",
            ["wiki/bob.md"] = "# Bob wiki"
        });

        // Local instance: one page of its own.
        InstanceLayout.CreateSkeleton(_local);
        File.WriteAllText(Path.Combine(_local, "wiki", "mine.md"), "# Mine\n\n#local");
        var localRepo = new PrimaryRepository(_local);
        localRepo.Init();
        localRepo.StageAll();
        localRepo.Commit("init", Author);

        _service = new SubscriptionService(_local);
    }

    public void Dispose()
    {
        foreach (var p in new[] { _aliceWork, _aliceBare, _bobWork, _bobBare, _local })
            GitTestHelpers.DeleteDirectory(p);
    }

    private static PrimaryRepository CreateInstance(string work, string bare, Dictionary<string, string> files)
    {
        GitTestHelpers.CreateBareRepository(bare);
        InstanceLayout.CreateSkeleton(work);
        foreach (var (path, content) in files)
        {
            var full = Path.Combine(work, path.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            File.WriteAllText(full, content);
        }
        var repo = new PrimaryRepository(work);
        repo.Init();
        repo.StageAll();
        repo.Commit("initial", Author);
        repo.AddRemote("origin", bare);
        repo.Push();
        return repo;
    }

    private static void AddTopicBranch(PrimaryRepository repo, string work, string branch, string file, string content)
    {
        var original = repo.CurrentBranch!;
        repo.CreateBranch(branch, checkout: true);
        var full = Path.Combine(work, file.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, content);
        repo.StageAll();
        repo.Commit($"topic {branch}", Author);
        repo.Checkout(original);
    }

    private Subscription Alice(SubscriptionFilter? filter = null, string? branch = null) =>
        new() { Name = "alice", RemoteUrl = _aliceBare, Branch = branch, Filter = filter ?? new SubscriptionFilter() };

    private Subscription Bob(SubscriptionFilter? filter = null) =>
        new() { Name = "bob", RemoteUrl = _bobBare, Filter = filter ?? new SubscriptionFilter() };

    // ── add / list / remove ──────────────────────────────────────────────────

    [Fact]
    public void Add_ClonesCache_PersistsSubscription_AndIgnoresCacheDir()
    {
        var result = _service.Add(Alice());

        Assert.True(result.Succeeded);
        Assert.True(result.Changed);
        Assert.True(_service.IsCached(result.Subscription));
        Assert.True(Directory.Exists(Path.Combine(_local, ".publictxt", "subscriptions", "alice", ".git")));

        var listed = Assert.Single(_service.List());
        Assert.Equal("alice", listed.Name);
        Assert.NotNull(listed.AddedAt);
        Assert.NotNull(listed.LastUpdatedAt);
        Assert.Equal(result.CommitSha, listed.LastCommitSha);

        Assert.Contains(".publictxt/", File.ReadAllText(Path.Combine(_local, ".gitignore")));
        Assert.True(new PrimaryRepository(_local).GetStatus().UntrackedCount > 0, "settings/subscriptions.json and .gitignore are new");
        var status = new PrimaryRepository(_local).GetStatus();
        Assert.Equal(2, status.UntrackedCount);
    }

    [Fact]
    public void Add_Twice_Throws_AndKeepsOne()
    {
        _service.Add(Alice());

        var ex = Assert.Throws<InvalidOperationException>(() => _service.Add(Alice()));

        Assert.Contains("already exists", ex.Message);
        Assert.Single(_service.List());
    }

    [Fact]
    public void Add_WithBadRemote_Throws_AndLeavesNoEntry()
    {
        var bad = new Subscription { Name = "bad", RemoteUrl = GitTestHelpers.TempPath("nowhere") };

        Assert.ThrowsAny<Exception>(() => _service.Add(bad));

        Assert.Empty(_service.List());
        Assert.Null(_service.Find("bad"));
    }

    [Fact]
    public void Add_InvalidSubscription_Throws()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => _service.Add(new Subscription { Name = "no spaces allowed", RemoteUrl = _aliceBare }));
        Assert.Contains("no spaces allowed", ex.Message);
    }

    [Fact]
    public void Remove_DeletesEntryAndCache()
    {
        _service.Add(Alice());
        var cache = _service.CachePathFor("alice");

        Assert.True(_service.Remove("ALICE"));

        Assert.Empty(_service.List());
        Assert.False(Directory.Exists(cache));
        Assert.False(_service.Remove("alice"));
    }

    // ── update ───────────────────────────────────────────────────────────────

    [Fact]
    public void Update_ReportsUnchanged_ThenChanged_AfterRemoteCommit()
    {
        _service.Add(Alice());
        var first = Assert.Single(_service.Update("alice"));
        Assert.True(first.Succeeded);
        Assert.False(first.Changed);

        File.WriteAllText(Path.Combine(_aliceWork, "wiki", "new.md"), "# New");
        var alice = new PrimaryRepository(_aliceWork);
        alice.StageAll();
        var commit = alice.Commit("new page", Author);
        alice.Push();

        var second = Assert.Single(_service.Update());
        Assert.True(second.Changed);
        Assert.Equal(commit.Sha, second.CommitSha);
        Assert.Equal(commit.Sha, _service.Find("alice")!.LastCommitSha);
        Assert.Contains(_service.Enumerate(_service.Find("alice")!), i => i.RelativePath == "wiki/new.md");
    }

    [Fact]
    public void Update_UnknownName_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => _service.Update("ghost"));
    }

    [Fact]
    public void Update_ReportsFailure_PerSubscription_WithoutThrowing()
    {
        _service.Add(Alice());
        _service.Add(Bob());
        GitTestHelpers.DeleteDirectory(_bobBare);

        var results = _service.Update();

        Assert.Equal(2, results.Count);
        Assert.True(results.Single(r => r.Subscription.Name == "alice").Succeeded);
        var bob = results.Single(r => r.Subscription.Name == "bob");
        Assert.False(bob.Succeeded);
        Assert.NotNull(bob.Error);
    }

    // ── content & filters ────────────────────────────────────────────────────

    [Fact]
    public void Enumerate_AppliesTypePathAndTagFilters()
    {
        var filter = new SubscriptionFilter
        {
            Types = [ContentType.Wiki],
            Paths = ["wiki/recipes/**"],
            ExcludeTags = ["draft"]
        };
        _service.Add(Alice(filter));

        var items = _service.Enumerate(_service.Find("alice")!).ToList();

        var only = Assert.Single(items);
        Assert.Equal("wiki/recipes/bread.md", only.RelativePath);
        Assert.Equal("alice", only.Source);
        Assert.Equal(ContentType.Wiki, only.Type);
    }

    [Fact]
    public void Enumerate_FollowsTopicBranch()
    {
        _service.Add(Alice(new SubscriptionFilter { Tags = ["recipes"] }, branch: "topic/soup"));

        var paths = _service.Enumerate(_service.Find("alice")!).Select(i => i.RelativePath).ToList();

        Assert.Contains("wiki/recipes/soup.md", paths);
        Assert.Contains("wiki/recipes/bread.md", paths);
        Assert.DoesNotContain("wiki/about.md", paths);
    }

    [Fact]
    public void Enumerate_BeforeUpdate_Throws()
    {
        var never = Alice();
        Assert.Throws<InvalidOperationException>(() => _service.Enumerate(never).ToList());
    }

    [Fact]
    public void BuildAggregate_CombinesLocalAndTwoSubscriptionsWithDifferentFilters()
    {
        _service.Add(Alice(new SubscriptionFilter { Types = [ContentType.Blog, ContentType.Wiki], ExcludeTags = ["draft"] }));
        _service.Add(Bob(new SubscriptionFilter { Types = [ContentType.Notes, ContentType.Blog] }));
        var local = InstanceContentReader.Open(_local);

        var agg = _service.BuildAggregate(local);

        Assert.Empty(agg.Failures);
        Assert.Equal(["wiki/mine.md"], agg.Local.Select(i => i.RelativePath));
        Assert.Equal(
            ["blog/2024/03/01/20240301.md", "wiki/about.md", "wiki/recipes/bread.md"],
            agg.From("alice").Select(i => i.RelativePath).OrderBy(p => p, StringComparer.Ordinal));
        Assert.Equal(
            ["blog/2024/04/01/20240401.md", "notes/idea.md"],
            agg.From("bob").Select(i => i.RelativePath).OrderBy(p => p, StringComparer.Ordinal));
        Assert.Equal(6, agg.Items.Count);

        var blogTimeline = agg.Timeline().Where(i => i.Type == ContentType.Blog).Select(i => i.Source).ToList();
        Assert.Equal(["bob", "alice"], blogTimeline);
        Assert.Contains("ideas", agg.Tags);
        Assert.DoesNotContain("draft", agg.Tags);
    }

    [Fact]
    public void BuildAggregate_ReportsUnfetchedSubscription_AsFailure()
    {
        _service.Add(Alice());
        GitTestHelpers.DeleteDirectory(_service.CachePathFor("alice"));

        var agg = _service.BuildAggregate(InstanceContentReader.Open(_local));

        var failure = Assert.Single(agg.Failures);
        Assert.Equal("alice", failure.Subscription.Name);
        Assert.Contains("not been fetched", failure.Message);
        Assert.Single(agg.Items);
    }
}
