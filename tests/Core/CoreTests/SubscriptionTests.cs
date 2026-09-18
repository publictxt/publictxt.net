using PublicTxt.Core.Content;
using PublicTxt.Core.Instances;
using PublicTxt.Core.Subscriptions;

namespace CoreTests;

public class SubscriptionFilterTests
{
    private static ContentItem Item(string path, ContentType type, params string[] tags) => new()
    {
        RelativePath = path,
        Type = type,
        Title = path,
        Tags = tags
    };

    [Fact]
    public void EmptyFilter_MatchesEverything()
    {
        var f = new SubscriptionFilter();
        Assert.True(f.IsEmpty);
        Assert.True(f.Matches(Item("wiki/a.md", ContentType.Wiki)));
        Assert.True(f.Matches(Item("blog/2024/01/01/x.md", ContentType.Blog, "t")));
        Assert.Equal("(everything)", f.Describe());
    }

    [Fact]
    public void Types_RestrictToListedTypes()
    {
        var f = new SubscriptionFilter { Types = [ContentType.Blog, ContentType.Notes] };

        Assert.True(f.MatchesPath("blog/x.md", ContentType.Blog));
        Assert.True(f.MatchesPath("notes/x.md", ContentType.Notes));
        Assert.False(f.MatchesPath("wiki/x.md", ContentType.Wiki));
    }

    [Theory]
    [InlineData("wiki/recipes/**", "wiki/recipes/bread.md", true)]
    [InlineData("wiki/recipes/**", "wiki/recipes/deep/cake.md", true)]
    [InlineData("wiki/recipes/**", "wiki/other.md", false)]
    [InlineData("wiki/recipes", "wiki/recipes/bread.md", true)]
    [InlineData("wiki/recipes/", "wiki/recipes/bread.md", true)]
    [InlineData("**/*.md", "README.md", true)]
    [InlineData("**/*.md", "media/x.png", false)]
    [InlineData("WIKI/**", "wiki/a.md", true)]
    [InlineData(@"wiki\**", "wiki/a.md", true)]
    public void Paths_UseGlobs(string glob, string path, bool expected)
    {
        var f = new SubscriptionFilter { Paths = [glob] };
        Assert.Equal(expected, f.MatchesPath(path, ContentType.Wiki));
    }

    [Fact]
    public void Paths_AnyIncludeMatches_ThenExcludesApply()
    {
        var f = new SubscriptionFilter { Paths = ["wiki/**", "blog/**"], ExcludePaths = ["wiki/drafts/**"] };

        Assert.True(f.MatchesPath("wiki/a.md", ContentType.Wiki));
        Assert.True(f.MatchesPath("blog/b.md", ContentType.Blog));
        Assert.False(f.MatchesPath("wiki/drafts/c.md", ContentType.Wiki));
        Assert.False(f.MatchesPath("notes/d.md", ContentType.Notes));
    }

    [Fact]
    public void ExcludeOnly_MatchesAllButExcluded()
    {
        var f = new SubscriptionFilter { ExcludePaths = ["**/private/**"] };

        Assert.True(f.MatchesPath("wiki/a.md", ContentType.Wiki));
        Assert.False(f.MatchesPath("wiki/private/a.md", ContentType.Wiki));
    }

    [Fact]
    public void Tags_RequireAny_AndExcludeAny_CaseInsensitive()
    {
        var f = new SubscriptionFilter { Tags = ["recipes", "baking"], ExcludeTags = ["draft"] };

        Assert.True(f.Matches(Item("w/a.md", ContentType.Wiki, "Recipes")));
        Assert.True(f.Matches(Item("w/b.md", ContentType.Wiki, "baking", "bread")));
        Assert.False(f.Matches(Item("w/c.md", ContentType.Wiki, "bread")));
        Assert.False(f.Matches(Item("w/d.md", ContentType.Wiki, "recipes", "DRAFT")));
        Assert.True(f.MatchesPath("w/d.md", ContentType.Wiki), "path pre-check ignores tags");
    }

    [Fact]
    public void AllCriteria_AreAnded()
    {
        var f = new SubscriptionFilter { Types = [ContentType.Wiki], Paths = ["wiki/recipes/**"], Tags = ["bread"] };

        Assert.True(f.Matches(Item("wiki/recipes/x.md", ContentType.Wiki, "bread")));
        Assert.False(f.Matches(Item("wiki/recipes/x.md", ContentType.Blog, "bread")));
        Assert.False(f.Matches(Item("wiki/x.md", ContentType.Wiki, "bread")));
        Assert.False(f.Matches(Item("wiki/recipes/x.md", ContentType.Wiki, "cake")));
        Assert.Equal("types=wiki paths=wiki/recipes/** tags=bread", f.Describe());
    }

    [Fact]
    public void Validate_RejectsRootedGlobs_ParentSegments_AndBlankTags()
    {
        var f = new SubscriptionFilter { Paths = ["/abs/**", "../up/**", ""], Tags = [" "] };

        var errors = f.Validate();

        Assert.Equal(4, errors.Count);
    }
}

public class SubscriptionTests
{
    [Theory]
    [InlineData("alice", true)]
    [InlineData("alice-notes.v2_x", true)]
    [InlineData("", false)]
    [InlineData("-lead", false)]
    [InlineData("has space", false)]
    [InlineData("a/b", false)]
    [InlineData("..", false)]
    public void IsValidName(string name, bool expected)
    {
        Assert.Equal(expected, Subscription.IsValidName(name));
    }

    [Fact]
    public void Validate_CollectsAllProblems()
    {
        var sub = new Subscription
        {
            Name = "bad name",
            RemoteUrl = "",
            Branch = "-x",
            Filter = new SubscriptionFilter { Paths = ["../x"] }
        };

        var errors = sub.Validate();

        Assert.Equal(4, errors.Count);
        Assert.All(errors, e => Assert.Contains("bad name", e));
    }

    [Fact]
    public void Validate_PassesForMinimalSubscription()
    {
        Assert.Empty(new Subscription { Name = "ok", RemoteUrl = "https://example.com/r.git" }.Validate());
    }
}

public class SubscriptionStoreTests : IDisposable
{
    private readonly string _root = Fixtures.TempDir("subs");
    private readonly SubscriptionStore _store = new();

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public void Load_ReturnsEmpty_WhenMissing()
    {
        Assert.Empty(_store.Load(_root));
        Assert.False(_store.Exists(_root));
    }

    [Fact]
    public void Save_ThenLoad_RoundTrips_SortedByName()
    {
        var subs = new[]
        {
            new Subscription
            {
                Name = "zed",
                RemoteUrl = "https://example.com/zed.git",
                Branch = "topic/x",
                Filter = new SubscriptionFilter { Types = [ContentType.Blog], Paths = ["blog/**"], Tags = ["a"], ExcludeTags = ["b"] },
                AddedAt = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero),
                LastCommitSha = "abc123"
            },
            new Subscription { Name = "alpha", RemoteUrl = "https://example.com/alpha.git" }
        };

        _store.Save(_root, subs);
        var loaded = _store.Load(_root);

        Assert.True(_store.Exists(_root));
        Assert.Equal(new[] { "alpha", "zed" }, loaded.Select(s => s.Name));
        var zed = loaded[1];
        Assert.Equal("topic/x", zed.Branch);
        Assert.Equal([ContentType.Blog], zed.Filter.Types);
        Assert.Equal(["blog/**"], zed.Filter.Paths);
        Assert.Equal(["a"], zed.Filter.Tags);
        Assert.Equal(["b"], zed.Filter.ExcludeTags);
        Assert.Equal(new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero), zed.AddedAt);
        Assert.Equal("abc123", zed.LastCommitSha);
        Assert.Null(loaded[0].Branch);
        Assert.True(loaded[0].Filter.IsEmpty);
    }

    [Fact]
    public void Save_WritesCamelCaseWithLowercaseEnums_OmittingNulls()
    {
        _store.Save(_root, [new Subscription { Name = "a", RemoteUrl = "u", Filter = new SubscriptionFilter { Types = [ContentType.Wiki] } }]);
        var json = File.ReadAllText(SubscriptionStore.FilePath(_root));

        Assert.Contains("\"remoteUrl\": \"u\"", json);
        Assert.Contains("\"wiki\"", json);
        Assert.DoesNotContain("\"branch\"", json);
        Assert.DoesNotContain("Wiki", json);
    }

    [Fact]
    public void Load_ToleratesComments_AndReadsPascalCaseEnums()
    {
        Directory.CreateDirectory(Path.Combine(_root, "settings"));
        File.WriteAllText(SubscriptionStore.FilePath(_root),
            "{ // c\n \"subscriptions\": [ { \"name\": \"x\", \"remoteUrl\": \"u\", \"filter\": { \"types\": [\"Blog\"] } }, ] }");

        var loaded = _store.Load(_root);

        Assert.Equal([ContentType.Blog], Assert.Single(loaded).Filter.Types);
    }

    [Fact]
    public void Load_Throws_OnInvalidJson_DuplicateNames_OrInvalidEntries()
    {
        Directory.CreateDirectory(Path.Combine(_root, "settings"));

        File.WriteAllText(SubscriptionStore.FilePath(_root), "{ nope");
        Assert.Throws<InvalidOperationException>(() => _store.Load(_root));

        File.WriteAllText(SubscriptionStore.FilePath(_root),
            "{ \"subscriptions\": [ { \"name\": \"A\", \"remoteUrl\": \"u\" }, { \"name\": \"a\", \"remoteUrl\": \"u\" } ] }");
        var ex = Assert.Throws<InvalidOperationException>(() => _store.Load(_root));
        Assert.Contains("Duplicate", ex.Message);

        File.WriteAllText(SubscriptionStore.FilePath(_root), "{ \"subscriptions\": [ { \"name\": \"bad name\", \"remoteUrl\": \"u\" } ] }");
        Assert.Throws<InvalidOperationException>(() => _store.Load(_root));
    }

    [Fact]
    public void Save_Throws_OnDuplicateNames()
    {
        var subs = new[]
        {
            new Subscription { Name = "dup", RemoteUrl = "u" },
            new Subscription { Name = "DUP", RemoteUrl = "u" }
        };
        Assert.Throws<InvalidOperationException>(() => _store.Save(_root, subs));
        Assert.False(_store.Exists(_root));
    }
}

public class AggregateCatalogTests
{
    private sealed class FakeSource(Dictionary<string, Func<IEnumerable<ContentItem>>> sources) : ISubscriptionContentSource
    {
        public IEnumerable<ContentItem> Enumerate(Subscription subscription) => sources[subscription.Name]();
    }

    private static ContentItem Item(string path, ContentType type, DateOnly? date = null, params string[] tags) => new()
    {
        RelativePath = path,
        Type = type,
        Title = Path.GetFileNameWithoutExtension(path),
        Date = date,
        Tags = tags,
        Body = "body of " + path
    };

    private static readonly InstanceContentReader Local = InstanceContentReader.Open(Fixtures.SampleInstance);

    [Fact]
    public void Build_CombinesLocalAndSubscribedItems_AttributingSource()
    {
        var subs = new[] { new Subscription { Name = "bob", RemoteUrl = "u" } };
        var source = new FakeSource(new()
        {
            ["bob"] = () => [Item("wiki/index.md", ContentType.Wiki, tags: "bobtag"), Item("blog/2025/01/01/p.md", ContentType.Blog, new DateOnly(2025, 1, 1))]
        });

        var agg = AggregateCatalog.Build(Local, subs, source);

        Assert.Equal(8, agg.Items.Count);
        Assert.Equal(6, agg.Local.Count());
        Assert.Equal(2, agg.From("BOB").Count());
        Assert.All(agg.From("bob"), i => Assert.Equal("bob", i.Source));
        Assert.All(agg.Local, i => Assert.True(i.IsLocal));
        Assert.Equal(2, agg.Items.Count(i => i.RelativePath == "wiki/index.md"));
        Assert.Contains("bobtag", agg.Tags);
        Assert.Contains("recipes", agg.Tags);
        Assert.Empty(agg.Failures);
    }

    [Fact]
    public void Build_RecordsFailures_AndContinues()
    {
        var subs = new[]
        {
            new Subscription { Name = "good", RemoteUrl = "u" },
            new Subscription { Name = "broken", RemoteUrl = "u" }
        };
        var source = new FakeSource(new()
        {
            ["good"] = () => [Item("notes/n.md", ContentType.Notes)],
            ["broken"] = () => throw new InvalidOperationException("cache missing")
        });

        var agg = AggregateCatalog.Build(Local, subs, source);

        Assert.Single(agg.From("good"));
        var failure = Assert.Single(agg.Failures);
        Assert.Equal("broken", failure.Subscription.Name);
        Assert.Equal("cache missing", failure.Message);
    }

    [Fact]
    public void Timeline_OrdersNewestFirst_UndatedLast()
    {
        var subs = new[] { new Subscription { Name = "s", RemoteUrl = "u" } };
        var source = new FakeSource(new()
        {
            ["s"] = () =>
            [
                Item("blog/old.md", ContentType.Blog, new DateOnly(2020, 1, 1)),
                Item("blog/new.md", ContentType.Blog, new DateOnly(2030, 1, 1)),
                Item("wiki/undated.md", ContentType.Wiki)
            ]
        });

        var timeline = AggregateCatalog.Build(Local, subs, source).Timeline().ToList();

        Assert.Equal("blog/new.md", timeline[0].RelativePath);
        Assert.Equal(new DateOnly(2024, 1, 15), timeline[1].Date);
        Assert.Equal("blog/old.md", timeline[3].RelativePath);
        Assert.All(timeline.Skip(4), i => Assert.Null(i.Date));
    }

    [Fact]
    public void Search_MatchesTitleTagsOrBody_CaseInsensitive()
    {
        var agg = AggregateCatalog.LocalOnly(Local);

        Assert.Contains(agg.Search("SOURDOUGH"), i => i.RelativePath == "wiki/recipes/sourdough.md");
        Assert.Contains(agg.Search("baking"), i => i.RelativePath == "wiki/recipes/sourdough.md");
        Assert.Contains(agg.Search("bake bread"), i => i.RelativePath == "notes/todo.md");
        Assert.Empty(agg.Search("zzz-not-there"));
        Assert.Equal(agg.Items.Count, agg.Search("  ").Count());
    }

    [Fact]
    public void WithSource_CopiesEverythingButSource()
    {
        var original = Item("wiki/a.md", ContentType.Wiki, new DateOnly(2024, 2, 2), "t");
        var copy = original.WithSource("x");

        Assert.Equal("x", copy.Source);
        Assert.Null(original.Source);
        Assert.Equal(original.RelativePath, copy.RelativePath);
        Assert.Equal(original.Tags, copy.Tags);
        Assert.Equal(original.Date, copy.Date);
        Assert.Equal(original.Body, copy.Body);
        Assert.Equal("Wiki: x:wiki/a.md", copy.ToString());
    }
}
