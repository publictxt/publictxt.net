using PublicTxt.Core.Content;

namespace CoreTests;

public class BlogPathConventionTests
{
    [Theory]
    [InlineData("blog/2023/12/17/20231217.md", 2023, 12, 17)]
    [InlineData("blog/2023/12/17/some-title.md", 2023, 12, 17)]
    [InlineData("2023/12/17/x.md", 2023, 12, 17)]
    [InlineData(@"blog\2024\01\02\post.md", 2024, 1, 2)]
    [InlineData("blog/20240102.md", 2024, 1, 2)]
    [InlineData("blog/archive/20240102.MD", 2024, 1, 2)]
    public void TryGetDate_ParsesDatedDirectoryOrFileName(string path, int y, int m, int d)
    {
        Assert.True(BlogPathConvention.TryGetDate(path, out var date));
        Assert.Equal(new DateOnly(y, m, d), date);
    }

    [Theory]
    [InlineData("blog/post.md")]
    [InlineData("blog/2023/12/x.md")]
    [InlineData("blog/2023/13/01/x.md")]
    [InlineData("blog/2023/02/30/x.md")]
    [InlineData("blog/12345678.md")]
    [InlineData("blog/2023-12-17.md")]
    public void TryGetDate_RejectsNonConformingOrInvalidDates(string path)
    {
        Assert.False(BlogPathConvention.TryGetDate(path, out _));
    }

    [Fact]
    public void DirectoryPrecedence_OverFileName()
    {
        Assert.True(BlogPathConvention.TryGetDate("blog/2023/12/17/20200101.md", out var date));
        Assert.Equal(new DateOnly(2023, 12, 17), date);
    }

    [Fact]
    public void PathBuilders_ProduceConventionalLayout()
    {
        var date = new DateOnly(2023, 12, 7);

        Assert.Equal("2023/12/07", BlogPathConvention.DirectoryFor(date));
        Assert.Equal("20231207.md", BlogPathConvention.MainPostFileName(date));
        Assert.Equal("2023/12/07/20231207.md", BlogPathConvention.MainPostPath(date));
        Assert.True(BlogPathConvention.TryGetDate(BlogPathConvention.MainPostPath(date), out var roundTrip));
        Assert.Equal(date, roundTrip);
    }
}
