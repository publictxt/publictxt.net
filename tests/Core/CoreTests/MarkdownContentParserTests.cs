using PublicTxt.Core.Content;

namespace CoreTests;

public class MarkdownContentParserTests
{
    private readonly MarkdownContentParser _parser = MarkdownContentParser.Default;

    // ── title ────────────────────────────────────────────────────────────────

    [Fact]
    public void Title_ComesFromFrontMatter_WhenPresent()
    {
        var item = _parser.Parse("wiki/page.md", ContentType.Wiki, "---\ntitle: From Front Matter\n---\n# Heading\n");
        Assert.Equal("From Front Matter", item.Title);
    }

    [Fact]
    public void Title_FallsBackToFirstH1()
    {
        var item = _parser.Parse("wiki/page.md", ContentType.Wiki, "intro\n\n## not this\n\n# The *Real* Title\n");
        Assert.Equal("The Real Title", item.Title);
    }

    [Fact]
    public void Title_FallsBackToFileName()
    {
        var item = _parser.Parse("wiki/my-page.md", ContentType.Wiki, "just text");
        Assert.Equal("my-page", item.Title);
    }

    // ── paths ────────────────────────────────────────────────────────────────

    [Fact]
    public void RelativePath_IsNormalisedToForwardSlashes()
    {
        var item = _parser.Parse(@"\wiki\sub\page.md", ContentType.Wiki, "x");

        Assert.Equal("wiki/sub/page.md", item.RelativePath);
        Assert.Equal("page.md", item.FileName);
        Assert.Equal("wiki/sub", item.Directory);
    }

    [Fact]
    public void Directory_IsEmpty_ForRootFile()
    {
        var item = _parser.Parse("README.md", ContentType.Unknown, "x");
        Assert.Equal(string.Empty, item.Directory);
    }

    // ── front matter & body ──────────────────────────────────────────────────

    [Fact]
    public void FrontMatter_IsParsed_AndStrippedFromBody()
    {
        var md = "---\ntitle: T\ntags: [a, b]\ndate: 2024-05-06\n---\n\nBody line\n";
        var item = _parser.Parse("notes/n.md", ContentType.Notes, md);

        Assert.Equal("T", item.FrontMatter.GetString("title"));
        Assert.Equal(new[] { "a", "b" }, item.FrontMatter.GetStringList("tags"));
        Assert.Equal(new DateOnly(2024, 5, 6), item.Date);
        Assert.Equal("Body line\n", item.Body);
    }

    [Fact]
    public void FrontMatter_IsEmpty_WhenAbsent()
    {
        var item = _parser.Parse("notes/n.md", ContentType.Notes, "# H\n\ntext");
        Assert.True(item.FrontMatter.IsEmpty);
        Assert.Equal("# H\n\ntext", item.Body);
    }

    [Fact]
    public void FrontMatter_Malformed_IsTreatedAsEmpty()
    {
        var item = _parser.Parse("notes/n.md", ContentType.Notes, "---\ntitle: [unclosed\n---\n# H\n");
        Assert.True(item.FrontMatter.IsEmpty);
        Assert.Equal("H", item.Title);
    }

    [Fact]
    public void FrontMatter_KeysAreCaseInsensitive()
    {
        var item = _parser.Parse("notes/n.md", ContentType.Notes, "---\nTitle: Cased\n---\n");
        Assert.Equal("Cased", item.Title);
    }

    // ── tags ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Tags_MergeFrontMatterAndInline_CaseInsensitiveDedupe()
    {
        var md = "---\ntags:\n  - Recipes\n  - baking\n---\nText with #recipes and #Sourdough and #baking/bread.\n";
        var item = _parser.Parse("wiki/x.md", ContentType.Wiki, md);

        Assert.Equal(new[] { "Recipes", "baking", "Sourdough", "baking/bread" }, item.Tags);
        Assert.True(item.HasTag("recipes"));
    }

    [Fact]
    public void Tags_FromCommaSeparatedString()
    {
        var item = _parser.Parse("wiki/x.md", ContentType.Wiki, "---\ntags: one, two ,three\n---\n");
        Assert.Equal(new[] { "one", "two", "three" }, item.Tags);
    }

    [Fact]
    public void Tags_IgnoreHeadingsCodeAndNumericOnly()
    {
        var md = "# Heading\n\n`#notatag` and\n\n```\n#alsonot\n```\n\nissue #123 but #v2 counts\n";
        var item = _parser.Parse("wiki/x.md", ContentType.Wiki, md);

        Assert.Equal(new[] { "v2" }, item.Tags);
    }

    [Fact]
    public void Tags_IgnoreUrlFragmentsAndEntities()
    {
        var md = "see https://example.com/page#section and &#169; and a/#b\n";
        var item = _parser.Parse("wiki/x.md", ContentType.Wiki, md);

        Assert.Empty(item.Tags);
    }

    // ── links ────────────────────────────────────────────────────────────────

    [Fact]
    public void Links_AreClassified()
    {
        var md = "[wiki](../wiki/page.md) [ext](https://example.com/x) [anchor](#top) " +
                 "[frag](page.md#sec) ![img](../media/pic.png) [mail](mailto:a@b.c)\n";
        var item = _parser.Parse("blog/2024/01/02/post.md", ContentType.Blog, md);

        Assert.Collection(item.Links,
            l => { Assert.Equal("../wiki/page.md", l.Target); Assert.Equal(ContentLinkKind.Internal, l.Kind); Assert.Equal("wiki", l.Text); },
            l => { Assert.Equal("https://example.com/x", l.Target); Assert.Equal(ContentLinkKind.External, l.Kind); },
            l => { Assert.Equal(string.Empty, l.Target); Assert.Equal(ContentLinkKind.Anchor, l.Kind); Assert.Equal("top", l.Fragment); },
            l => { Assert.Equal("page.md", l.Target); Assert.Equal(ContentLinkKind.Internal, l.Kind); Assert.Equal("sec", l.Fragment); },
            l => { Assert.Equal("../media/pic.png", l.Target); Assert.True(l.IsImage); Assert.Equal("img", l.Text); },
            l => { Assert.Equal("mailto:a@b.c", l.Target); Assert.Equal(ContentLinkKind.External, l.Kind); });

        Assert.Equal(3, item.InternalLinks.Count());
    }

    [Fact]
    public void Links_WindowsDriveLetter_IsNotTreatedAsScheme()
    {
        var link = MarkdownContentParser.Classify(@"c:\file.md", null, false);
        Assert.Equal(ContentLinkKind.Internal, link.Kind);
    }

    [Fact]
    public void Links_ReferenceStyle_AreIncluded()
    {
        var md = "see [the page][ref]\n\n[ref]: other.md\n";
        var item = _parser.Parse("wiki/x.md", ContentType.Wiki, md);

        var link = Assert.Single(item.Links);
        Assert.Equal("other.md", link.Target);
        Assert.Equal("the page", link.Text);
    }

    // ── dates ────────────────────────────────────────────────────────────────

    [Fact]
    public void Date_ForBlog_ComesFromPath_WhenNoFrontMatter()
    {
        var item = _parser.Parse("blog/2023/12/17/20231217.md", ContentType.Blog, "# Post");
        Assert.Equal(new DateOnly(2023, 12, 17), item.Date);
    }

    [Fact]
    public void Date_FrontMatterWins_OverPath()
    {
        var item = _parser.Parse("blog/2023/12/17/x.md", ContentType.Blog, "---\ndate: 2020-01-01\n---\n");
        Assert.Equal(new DateOnly(2020, 1, 1), item.Date);
    }

    [Fact]
    public void Date_IsNull_ForNonBlogWithoutFrontMatter()
    {
        var item = _parser.Parse("wiki/2023/12/17/x.md", ContentType.Wiki, "# not a post");
        Assert.Null(item.Date);
    }

    [Fact]
    public void ModifiedAt_IsPassedThrough()
    {
        var when = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var item = _parser.Parse("wiki/x.md", ContentType.Wiki, "x", when);
        Assert.Equal(when, item.ModifiedAt);
    }
}
