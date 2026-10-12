using PublicTxt.Core.Content;
using PublicTxt.Core.Instances;

namespace CoreTests;

public class HtmlContentRendererTests
{
    private static (HtmlContentRenderer Renderer, ContentCatalog Catalog) Build(string pageMarkdown, ContentUrls? urls = null)
    {
        var source = new InMemoryFileSource();
        source.Add("wiki/page.md", pageMarkdown);
        source.Add("wiki/My Page.md", "# My Page");
        source.Add("wiki/sub/other.md", "# Other");
        source.Add("media/logo.png", "");
        var catalog = InstanceContentReader.Open(source).BuildCatalog();
        return (new HtmlContentRenderer(catalog, urls), catalog);
    }

    private static string Render(string pageMarkdown, ContentUrls? urls = null)
    {
        var (renderer, catalog) = Build(pageMarkdown, urls);
        return renderer.Render(catalog.Get("wiki/page.md")!);
    }

    [Fact]
    public void InternalLinks_AreRewrittenToPageUrls_KeepingFragments()
    {
        var html = Render("[a](sub/other.md) [b](My%20Page.md#intro) [c](sub/other) [d](/wiki/sub/other.md)");

        Assert.Contains("href=\"/wiki/sub/other\"", html);
        Assert.Contains("href=\"/wiki/My%20Page#intro\"", html);
        Assert.Equal(3, CountOf(html, "href=\"/wiki/sub/other\""));
    }

    [Fact]
    public void LinksToFiles_UseFileUrls()
    {
        var html = Render("![logo](../media/logo.png)");

        Assert.Contains("src=\"/media/logo.png\"", html);
    }

    [Fact]
    public void BrokenLinks_AreMarked_AndEscapingLinksGoNowhere()
    {
        var html = Render("[gone](missing.md) [out](../../escape.md)");

        Assert.Contains("href=\"/wiki/missing\" class=\"broken-link\"", html);
        Assert.Contains("href=\"#\" class=\"broken-link\"", html);
    }

    [Fact]
    public void CustomUrls_AreUsed()
    {
        var urls = new ContentUrls(p => "app://page/" + p, p => "blob:" + p);
        var html = Render("[a](sub/other.md) ![l](../media/logo.png)", urls);

        Assert.Contains("href=\"app://page/wiki/sub/other.md\"", html);
        Assert.Contains("src=\"blob:media/logo.png\"", html);
    }

    [Fact]
    public void SafeExternalLinks_AreKept_AndMarked()
    {
        var html = Render("[x](https://example.com/a) [m](mailto:a@b.c) <https://example.org> https://bare.example");

        Assert.Contains("href=\"https://example.com/a\" class=\"external-link\" rel=\"noopener noreferrer\"", html);
        Assert.Contains("href=\"mailto:a@b.c\"", html);
        Assert.Contains("href=\"https://example.org\"", html);
        Assert.Contains("href=\"https://bare.example\"", html);
    }

    [Theory]
    [InlineData("[x](javascript:alert(1))")]
    [InlineData("[x](JavaScript:alert(1))")]
    [InlineData("[x](&#106;avascript:alert(1))")]
    [InlineData("[x](data:text/html;base64,PHNjcmlwdD4=)")]
    [InlineData("[x][r]\n\n[r]: javascript:alert(1)")]
    [InlineData("![x](javascript:alert(1))")]
    [InlineData("<javascript:alert(1)>")]
    [InlineData("<vbscript:msgbox(1)>")]
    public void UnsafeSchemes_NeverReachTheHtml(string markdown)
    {
        var html = Render(markdown);

        Assert.DoesNotContain("href=\"javascript:", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("src=\"javascript:", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("=\"data:", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("=\"vbscript:", html, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("<script>alert(1)</script>", "<script>")]
    [InlineData("<img src=x onerror=alert(1)>", "<img src=x")]
    [InlineData("hi <b onclick=\"x()\">bold</b>", "<b onclick")]
    [InlineData("# Title {onclick=alert(1)}", "onclick=\"alert")]
    public void RawHtml_AndAttributeSyntax_AreNotRendered(string markdown, string forbidden)
    {
        Assert.DoesNotContain(forbidden, Render(markdown));
    }

    [Fact]
    public void Headings_GetIds_SoFragmentsWork()
    {
        Assert.Contains("<h2 id=\"getting-started\">", Render("## Getting Started"));
    }

    [Fact]
    public void Fixture_RendersWithoutUnresolvedMarkdownLinks()
    {
        var catalog = InstanceContentReader.Open(Fixtures.SampleInstance).BuildCatalog();
        var html = new HtmlContentRenderer(catalog).Render(catalog.Get("wiki/recipes/sourdough.md")!);

        Assert.DoesNotContain(".md\"", html);
        Assert.Contains("href=\"/wiki/recipes/no-ext-page\"", html);
        Assert.Contains("href=\"/notes/todo\"", html);
    }

    private static int CountOf(string text, string value) =>
        (text.Length - text.Replace(value, string.Empty).Length) / value.Length;
}
