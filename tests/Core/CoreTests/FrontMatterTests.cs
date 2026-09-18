using PublicTxt.Core.Content;

namespace CoreTests;

public class FrontMatterTests
{
    private static FrontMatter Make(params (string key, object? value)[] pairs) =>
        new(pairs.ToDictionary(p => p.key, p => p.value, StringComparer.OrdinalIgnoreCase));

    [Fact]
    public void Empty_HasNoValues()
    {
        Assert.True(FrontMatter.Empty.IsEmpty);
        Assert.Null(FrontMatter.Empty.GetString("title"));
        Assert.Empty(FrontMatter.Empty.GetStringList("tags"));
        Assert.Null(FrontMatter.Empty.GetDate("date"));
    }

    [Fact]
    public void GetString_ReturnsScalars_ButNotLists()
    {
        var fm = Make(("title", "Hello"), ("count", 3), ("tags", new List<object> { "a" }));

        Assert.Equal("Hello", fm.GetString("title"));
        Assert.Equal("3", fm.GetString("count"));
        Assert.Null(fm.GetString("tags"));
        Assert.Null(fm.GetString("missing"));
    }

    [Fact]
    public void GetStringList_HandlesList_CommaString_SpaceString_AndScalar()
    {
        Assert.Equal(new[] { "a", "b" }, Make(("k", new List<object> { "a", " b " })).GetStringList("k"));
        Assert.Equal(new[] { "a", "b" }, Make(("k", "a, b")).GetStringList("k"));
        Assert.Equal(new[] { "a", "b" }, Make(("k", "a b")).GetStringList("k"));
        Assert.Equal(new[] { "one" }, Make(("k", "one")).GetStringList("k"));
        Assert.Equal(new[] { "7" }, Make(("k", 7)).GetStringList("k"));
    }

    [Fact]
    public void GetStringList_SkipsBlankEntries()
    {
        var fm = Make(("k", new List<object?> { "a", null, "  ", "b" }));
        Assert.Equal(new[] { "a", "b" }, fm.GetStringList("k"));
    }

    [Fact]
    public void GetDate_ParsesIsoDate_AndDateTime()
    {
        Assert.Equal(new DateOnly(2024, 2, 3), Make(("d", "2024-02-03")).GetDate("d"));
        Assert.Equal(new DateOnly(2024, 2, 3), Make(("d", "2024-02-03T10:00:00Z")).GetDate("d"));
        Assert.Null(Make(("d", "not a date")).GetDate("d"));
    }

    [Fact]
    public void Keys_AreCaseInsensitive()
    {
        var fm = Make(("Title", "x"));
        Assert.True(fm.ContainsKey("title"));
        Assert.Equal("x", fm.GetString("TITLE"));
    }
}
