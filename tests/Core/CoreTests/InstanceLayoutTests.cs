using PublicTxt.Core.Content;
using PublicTxt.Core.Instances;
using PublicTxt.Core.Models;

namespace CoreTests;

internal static class Fixtures
{
    /// <summary>Locates <c>tests/fixtures/sample-instance</c> by walking up from the test assembly directory.</summary>
    public static string SampleInstance
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
            throw new DirectoryNotFoundException("tests/fixtures/sample-instance not found above " + AppContext.BaseDirectory);
        }
    }

    public static string TempDir(string prefix)
    {
        var path = Path.Combine(Path.GetTempPath(), $"publictxt-core-{prefix}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}

public class InstanceLayoutTests : IDisposable
{
    private readonly string _tmp = Fixtures.TempDir("layout");

    public void Dispose()
    {
        if (Directory.Exists(_tmp)) Directory.Delete(_tmp, recursive: true);
    }

    [Fact]
    public void IsInstance_TrueForFixture_FalseForEmptyDir()
    {
        Assert.True(InstanceLayout.IsInstance(Fixtures.SampleInstance));
        Assert.False(InstanceLayout.IsInstance(_tmp));
        Assert.False(InstanceLayout.IsInstance(""));
    }

    [Fact]
    public void ContentPaths_AreNormalised()
    {
        var settings = new TxtInstanceSettings { WikiPath = @".\Wiki\", BlogPath = "posts/blog/" };

        var paths = InstanceLayout.ContentPaths(settings);

        Assert.Equal("Wiki", paths[ContentType.Wiki]);
        Assert.Equal("posts/blog", paths[ContentType.Blog]);
        Assert.Equal("notes", paths[ContentType.Notes]);
    }

    [Theory]
    [InlineData("wiki/page.md", ContentType.Wiki)]
    [InlineData("wiki", ContentType.Wiki)]
    [InlineData("WIKI/Page.md", ContentType.Wiki)]
    [InlineData("blog/2024/01/01/x.md", ContentType.Blog)]
    [InlineData("media/pic.png", ContentType.Media)]
    [InlineData("settings/instance.json", ContentType.Settings)]
    [InlineData("wikipedia/x.md", ContentType.Unknown)]
    [InlineData("README.md", ContentType.Unknown)]
    public void TypeOf_MatchesDirectoryPrefix(string path, ContentType expected)
    {
        Assert.Equal(expected, InstanceLayout.TypeOf(TxtInstanceSettings.CreateDefault(), path));
    }

    [Fact]
    public void TypeOf_LongestPrefixWins_WhenNested()
    {
        var settings = new TxtInstanceSettings { WikiPath = "content", NotesPath = "content/notes" };

        Assert.Equal(ContentType.Notes, InstanceLayout.TypeOf(settings, "content/notes/a.md"));
        Assert.Equal(ContentType.Wiki, InstanceLayout.TypeOf(settings, "content/a.md"));
    }

    [Fact]
    public void CreateSkeleton_CreatesDirectoriesAndSettings()
    {
        var root = Path.Combine(_tmp, "new");

        var settings = InstanceLayout.CreateSkeleton(root);

        Assert.True(InstanceLayout.IsInstance(root));
        foreach (var dir in InstanceLayout.ContentPaths(settings).Values)
        {
            Assert.True(Directory.Exists(Path.Combine(root, dir)), dir);
        }
        Assert.True(File.Exists(Path.Combine(root, "wiki", ".gitkeep")));
        Assert.False(File.Exists(Path.Combine(root, "settings", ".gitkeep")), "settings dir is not empty so gets no .gitkeep");
    }

    [Fact]
    public void CreateSkeleton_IsIdempotent_AndKeepsExistingSettings()
    {
        var root = Path.Combine(_tmp, "twice");
        InstanceLayout.CreateSkeleton(root, new TxtInstanceSettings { WikiPath = "pages" });
        File.WriteAllText(Path.Combine(root, "pages", "home.md"), "# home");

        var second = InstanceLayout.CreateSkeleton(root, new TxtInstanceSettings { WikiPath = "ignored" });

        Assert.Equal("pages", second.WikiPath);
        Assert.True(File.Exists(Path.Combine(root, "pages", "home.md")));
        Assert.False(Directory.Exists(Path.Combine(root, "ignored")));
    }

    [Fact]
    public void CreateSkeleton_RejectsInvalidSettings()
    {
        var root = Path.Combine(_tmp, "bad");
        var settings = new TxtInstanceSettings { WikiPath = "../escape" };

        Assert.Throws<InvalidOperationException>(() => InstanceLayout.CreateSkeleton(root, settings));
        Assert.False(Directory.Exists(root));
    }

    [Fact]
    public void Validate_ReportsMissingRoot_AndSettingsErrors()
    {
        var issues = InstanceLayout.Validate(Path.Combine(_tmp, "nope"), new TxtInstanceSettings { BlogPath = "wiki" });

        Assert.Contains(issues, i => i.Contains("does not exist"));
        Assert.Contains(issues, i => i.Contains("Duplicate"));
    }

    [Theory]
    [InlineData(@".\a\b\", "a/b")]
    [InlineData("/a/b/", "a/b")]
    [InlineData("  a  ", "a")]
    [InlineData("", "")]
    public void Normalise_StripsSlashesAndDotPrefix(string input, string expected)
    {
        Assert.Equal(expected, InstanceLayout.Normalise(input));
    }
}

public class TxtInstanceSettingsStoreTests : IDisposable
{
    private readonly string _tmp = Fixtures.TempDir("settings");
    private readonly TxtInstanceSettingsStore _store = new();

    public void Dispose()
    {
        if (Directory.Exists(_tmp)) Directory.Delete(_tmp, recursive: true);
    }

    [Fact]
    public void Load_ReturnsDefaults_WhenFileMissing()
    {
        var settings = _store.Load(_tmp);
        Assert.Equal("wiki", settings.WikiPath);
        Assert.False(_store.Exists(_tmp));
    }

    [Fact]
    public void Save_ThenLoad_RoundTrips()
    {
        var original = new TxtInstanceSettings
        {
            WikiPath = "pages",
            AutoSync = true,
            SyncIntervalMinutes = 5,
            DefaultBranch = "trunk"
        };

        _store.Save(_tmp, original);
        var loaded = _store.Load(_tmp);

        Assert.True(_store.Exists(_tmp));
        Assert.Equal("pages", loaded.WikiPath);
        Assert.True(loaded.AutoSync);
        Assert.Equal(5, loaded.SyncIntervalMinutes);
        Assert.Equal("trunk", loaded.DefaultBranch);
        Assert.Equal("blog", loaded.BlogPath);
    }

    [Fact]
    public void Save_WritesCamelCaseJson()
    {
        _store.Save(_tmp, TxtInstanceSettings.CreateDefault());
        var json = File.ReadAllText(InstanceLayout.SettingsFilePath(_tmp));

        Assert.Contains("\"wikiPath\": \"wiki\"", json);
        Assert.DoesNotContain("WikiPath", json);
    }

    [Fact]
    public void Load_ReadsFixtureSettings()
    {
        var settings = _store.Load(Fixtures.SampleInstance);
        Assert.Equal("blog", settings.BlogPath);
        Assert.Equal("main", settings.DefaultBranch);
    }

    [Fact]
    public void Load_ToleratesCommentsAndUnknownKeys()
    {
        Directory.CreateDirectory(Path.Combine(_tmp, "settings"));
        File.WriteAllText(InstanceLayout.SettingsFilePath(_tmp),
            "{\n  // comment\n  \"wikiPath\": \"w\",\n  \"futureKey\": 1,\n}\n");

        Assert.Equal("w", _store.Load(_tmp).WikiPath);
    }

    [Fact]
    public void Load_Throws_OnInvalidJson()
    {
        Directory.CreateDirectory(Path.Combine(_tmp, "settings"));
        File.WriteAllText(InstanceLayout.SettingsFilePath(_tmp), "{ not json");

        Assert.Throws<InvalidOperationException>(() => _store.Load(_tmp));
    }

    [Fact]
    public void Load_Throws_WhenSettingsInvalid()
    {
        Directory.CreateDirectory(Path.Combine(_tmp, "settings"));
        File.WriteAllText(InstanceLayout.SettingsFilePath(_tmp), "{ \"wikiPath\": \"blog\" }");

        var ex = Assert.Throws<InvalidOperationException>(() => _store.Load(_tmp));
        Assert.Contains("Duplicate", ex.Message);
    }

    [Fact]
    public void Save_Throws_WhenSettingsInvalid()
    {
        Assert.Throws<InvalidOperationException>(() => _store.Save(_tmp, new TxtInstanceSettings { NotesPath = "" }));
    }
}
