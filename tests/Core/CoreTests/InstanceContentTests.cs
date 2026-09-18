using PublicTxt.Core.Content;
using PublicTxt.Core.Instances;
using PublicTxt.Core.Models;

namespace CoreTests;

public class InstanceContentReaderTests
{
    private readonly InstanceContentReader _reader = InstanceContentReader.Open(Fixtures.SampleInstance);

    [Fact]
    public void Open_LoadsFixtureSettings()
    {
        Assert.Equal("wiki", _reader.Settings.WikiPath);
        Assert.Equal(Path.GetFullPath(Fixtures.SampleInstance), _reader.RootPath);
    }

    [Fact]
    public void EnumerateFiles_ListsAllFilesUnderType_SkippingDotEntries()
    {
        var wiki = _reader.EnumerateFiles(ContentType.Wiki).ToList();

        Assert.Equal(new[] { "wiki/index.md", "wiki/recipes/no-ext-page.md", "wiki/recipes/sourdough.md" }, wiki);
        Assert.Equal(new[] { "media/logo.png" }, _reader.EnumerateFiles(ContentType.Media));
        Assert.Empty(_reader.EnumerateFiles(ContentType.Tags));
        Assert.Empty(_reader.EnumerateFiles(ContentType.Unknown));
    }

    [Fact]
    public void Enumerate_ParsesMarkdownWithTypeAndDate()
    {
        var posts = _reader.Enumerate(ContentType.Blog).ToList();

        Assert.Equal(2, posts.Count);
        Assert.All(posts, p => Assert.Equal(ContentType.Blog, p.Type));
        Assert.All(posts, p => Assert.Equal(new DateOnly(2024, 1, 15), p.Date));
        Assert.Contains(posts, p => p.Title == "First Post" && p.HasTag("blog"));
        Assert.Contains(posts, p => p.Title == "Second Post" && p.HasTag("meta"));
        Assert.All(posts, p => Assert.NotNull(p.ModifiedAt));
    }

    [Fact]
    public void Enumerate_NonMarkdownType_YieldsNothing()
    {
        Assert.Empty(_reader.Enumerate(ContentType.Media));
    }

    [Fact]
    public void EnumerateAll_CoversEveryMarkdownType_ButNotRootOrHiddenFiles()
    {
        var paths = _reader.EnumerateAll().Select(i => i.RelativePath).ToList();

        Assert.Contains("wiki/index.md", paths);
        Assert.Contains("blog/2024/01/15/20240115.md", paths);
        Assert.Contains("notes/todo.md", paths);
        Assert.DoesNotContain("README.md", paths);
        Assert.DoesNotContain(paths, p => p.Contains(".obsidian"));
        Assert.Equal(6, paths.Count);
    }

    [Fact]
    public void Read_ReturnsItem_ForExistingMarkdown()
    {
        var item = _reader.Read(@"wiki\index.md");

        Assert.NotNull(item);
        Assert.Equal("Home", item!.Title);
        Assert.Equal(ContentType.Wiki, item.Type);
        Assert.Equal("wiki/index.md", item.RelativePath);
    }

    [Fact]
    public void Read_ReturnsNull_ForMissingOrNonMarkdown()
    {
        Assert.Null(_reader.Read("wiki/nope.md"));
        Assert.Null(_reader.Read("media/logo.png"));
    }

    [Fact]
    public void Read_Throws_WhenPathEscapesRoot()
    {
        Assert.Throws<UnauthorizedAccessException>(() => _reader.Read("../outside.md"));
        Assert.Throws<UnauthorizedAccessException>(() => _reader.FileExists("../../x"));
    }

    [Fact]
    public void FileExists_ChecksAnyFile()
    {
        Assert.True(_reader.FileExists("media/logo.png"));
        Assert.True(_reader.FileExists("settings/instance.json"));
        Assert.False(_reader.FileExists("media/missing.png"));
    }

    [Fact]
    public void Enumerate_WithMissingDirectory_IsEmpty()
    {
        var reader = new InstanceContentReader(Fixtures.SampleInstance, new TxtInstanceSettings { WikiPath = "not-there" });
        Assert.Empty(reader.Enumerate(ContentType.Wiki));
    }
}

public class ContentCatalogTests
{
    private readonly ContentCatalog _catalog = InstanceContentReader.Open(Fixtures.SampleInstance).BuildCatalog();

    [Fact]
    public void Items_AndLookups()
    {
        Assert.Equal(6, _catalog.Items.Count);
        Assert.NotNull(_catalog.Get("WIKI/Index.md"));
        Assert.Null(_catalog.Get("wiki/missing-page.md"));
        Assert.Equal(3, _catalog.OfType(ContentType.Wiki).Count());
        Assert.Equal(2, _catalog.WithTag("Recipes").Count());
    }

    [Fact]
    public void Tags_AreDistinctSortedCaseInsensitive()
    {
        Assert.Equal(new[] { "baking", "blog", "home", "meta", "recipes", "todo" }, _catalog.Tags);
    }

    [Fact]
    public void Links_ResolveRelative_RootRelative_Encoded_AndExtensionless()
    {
        var from = _catalog.LinksFrom("wiki/recipes/sourdough.md").ToList();

        Assert.Collection(from,
            l => { Assert.Equal("wiki/index.md", l.ResolvedPath); Assert.True(l.Exists); Assert.NotNull(l.Target); },
            l => { Assert.Equal("blog/2024/01/15/20240115.md", l.ResolvedPath); Assert.True(l.Exists); },
            l => { Assert.Equal("wiki/recipes/no-ext-page.md", l.ResolvedPath); Assert.True(l.Exists); Assert.Equal("No Extension Target", l.Target!.Title); },
            l => { Assert.Equal("notes/todo.md", l.ResolvedPath); Assert.True(l.Exists); },
            l => { Assert.Equal("wiki/index.md", l.ResolvedPath); Assert.True(l.Exists); });
    }

    [Fact]
    public void Links_ToNonMarkdownFiles_ExistWithoutTarget()
    {
        var image = _catalog.LinksFrom("wiki/index.md").Single(l => l.Link.IsImage);

        Assert.Equal("media/logo.png", image.ResolvedPath);
        Assert.True(image.Exists);
        Assert.Null(image.Target);
    }

    [Fact]
    public void BrokenLinks_AreReported_IncludingRootEscapes()
    {
        var broken = _catalog.BrokenLinks.ToList();

        Assert.Equal(2, broken.Count);
        Assert.Contains(broken, l => l.ResolvedPath == "wiki/missing-page.md" && l.Source.RelativePath == "wiki/index.md");
        Assert.Contains(broken, l => l.ResolvedPath is null && l.Link.Target.Contains("escape.md"));
    }

    [Fact]
    public void Backlinks_ListSourcesLinkingToAPage()
    {
        var back = _catalog.Backlinks("wiki/index.md").Select(i => i.RelativePath).ToList();

        Assert.Equal(new[] { "wiki/recipes/sourdough.md" }, back);
        Assert.Equal(new[] { "notes/todo.md" }, _catalog.Backlinks("blog/2024/01/15/second-post.md").Select(i => i.RelativePath));
        Assert.Equal(new[] { "wiki/recipes/sourdough.md" }, _catalog.Backlinks("wiki/recipes/no-ext-page.md").Select(i => i.RelativePath));
        Assert.Empty(_catalog.Backlinks("wiki/missing-page.md"));
    }

    [Theory]
    [InlineData("wiki", "page.md", "wiki/page.md")]
    [InlineData("wiki/recipes", "../index.md", "wiki/index.md")]
    [InlineData("wiki", "./a/./b.md", "wiki/a/b.md")]
    [InlineData("wiki", "/notes/x.md", "notes/x.md")]
    [InlineData("", "README.md", "README.md")]
    [InlineData("wiki", "my%20page.md", "wiki/my page.md")]
    [InlineData("wiki", @"sub\page.md", "wiki/sub/page.md")]
    [InlineData("wiki", "../../escape.md", null)]
    [InlineData("", "..", null)]
    public void ResolveTarget_NormalisesPaths(string sourceDir, string target, string? expected)
    {
        Assert.Equal(expected, ContentCatalog.ResolveTarget(sourceDir, target));
    }
}
