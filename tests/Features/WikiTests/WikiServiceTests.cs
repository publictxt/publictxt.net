using PublicTxt.Core.Instances;
using PublicTxt.Core.Models;
using PublicTxt.Wiki;

namespace WikiTests;

public class WikiServiceTests
{
    private readonly WikiService _wiki = WikiService.Open(InstanceContentReader.Open(Sample()));

    /// <summary>A small wiki in memory; default settings put it under <c>wiki/</c>.</summary>
    private static InMemoryFileSource Sample()
    {
        var source = new InMemoryFileSource();
        source.Add("wiki/index.md", "# Welcome\n\nSee [Sourdough](recipes/sourdough.md) and [Apple](apple.md).");
        source.Add("wiki/apple.md", "---\ntitle: Apple Pie\ntags: [baking, fruit]\n---\nUses [bread](recipes/sourdough).");
        source.Add("wiki/recipes/sourdough.md", "# Sourdough\n\nA #baking page. [Home](../index.md)");
        source.Add("wiki/recipes/apple.md", "# Apple Notes");
        source.Add("wiki/2024-plans.md", "# 2024 Plans");
        source.Add("wiki/.obsidian/workspace.md", "# Hidden");
        source.Add("notes/todo.md", "# Todo\n\n[pie](../wiki/apple.md)");
        return source;
    }

    [Fact]
    public void Pages_AreWikiOnly_OrderedByTitle()
    {
        Assert.Equal(
            new[] { "2024 Plans", "Apple Notes", "Apple Pie", "Sourdough", "Welcome" },
            _wiki.Pages.Select(p => p.Title));
    }

    [Fact]
    public void Home_PrefersIndex_ThenHome_ThenReadme()
    {
        Assert.Equal("wiki/index.md", _wiki.Home?.RelativePath);

        var source = new InMemoryFileSource();
        source.Add("wiki/README.md", "# Readme");
        source.Add("wiki/Home.md", "# Home");
        Assert.Equal("wiki/Home.md", WikiService.Open(InstanceContentReader.Open(source)).Home?.RelativePath);

        Assert.Null(WikiService.Open(InstanceContentReader.Open(new InMemoryFileSource())).Home);
    }

    [Theory]
    [InlineData("wiki/recipes/sourdough.md", "wiki/recipes/sourdough.md")] // instance-relative path
    [InlineData("wiki/recipes/sourdough", "wiki/recipes/sourdough.md")]    // extensionless
    [InlineData("recipes/sourdough", "wiki/recipes/sourdough.md")]         // wiki-relative
    [InlineData("/WIKI/Recipes/Sourdough/", "wiki/recipes/sourdough.md")]  // case and slashes
    [InlineData("sourdough", "wiki/recipes/sourdough.md")]                 // file name anywhere in the wiki
    [InlineData("apple", "wiki/apple.md")]                                 // wiki-relative path beats file name
    [InlineData("apple notes", "wiki/recipes/apple.md")]                   // title
    [InlineData("", "wiki/index.md")]                                      // empty → home
    public void Find_ResolvesNamesAndPaths(string query, string expected)
    {
        Assert.Equal(expected, _wiki.Find(query)?.RelativePath);
    }

    [Theory]
    [InlineData("notes/todo.md")]  // exists, but not a wiki page
    [InlineData("todo")]
    [InlineData("missing")]
    [InlineData("recipes/missing")]
    [InlineData(".obsidian/workspace")]
    public void Find_ReturnsNull_ForNonWikiOrMissing(string query)
    {
        Assert.Null(_wiki.Find(query));
    }

    [Fact]
    public void Backlinks_IncludeAnyContentType_OrderedByTitle()
    {
        var pie = _wiki.Find("wiki/apple.md")!;

        Assert.Equal(new[] { "Todo", "Welcome" }, _wiki.Backlinks(pie).Select(p => p.Title));
        Assert.Equal(new[] { "Apple Pie", "Welcome" }, _wiki.Backlinks(_wiki.Find("sourdough")!).Select(p => p.Title));
        Assert.Empty(_wiki.Backlinks(_wiki.Find("2024-plans")!));
    }

    [Fact]
    public void AlphabeticalIndex_GroupsByFirstLetter_NonLettersFirst()
    {
        var index = _wiki.AlphabeticalIndex();

        Assert.Equal(new[] { "#", "A", "S", "W" }, index.Select(s => s.Heading));
        Assert.Equal(new[] { "Apple Notes", "Apple Pie" }, index[1].Pages.Select(p => p.Title));
    }

    [Fact]
    public void TagIndex_ListsWikiTags_WithTheirPages()
    {
        var index = _wiki.TagIndex();

        Assert.Equal(new[] { "baking", "fruit" }, index.Select(s => s.Heading));
        Assert.Equal(new[] { "Apple Pie", "Sourdough" }, index[0].Pages.Select(p => p.Title));
    }

    [Fact]
    public void CustomWikiPath_IsHonoured()
    {
        var source = new InMemoryFileSource();
        source.Add("kb/index.md", "# KB Home");
        source.Add("kb/topic.md", "# Topic");
        var reader = new InstanceContentReader(source, new TxtInstanceSettings { WikiPath = "kb/" });
        var wiki = WikiService.Open(reader);

        Assert.Equal("kb", wiki.WikiPath);
        Assert.Equal("kb/index.md", wiki.Home?.RelativePath);
        Assert.Equal("kb/topic.md", wiki.Find("topic")?.RelativePath);
    }
}
