using PublicTxt.Core.Subscriptions;

namespace CoreTests;

public class RepoUrlTests
{
    // Same cases as obsidian-publictxt tests/repo-url.test.ts, so both apps agree.
    [Theory]
    [InlineData("https://github.com/Owner/Repo")]
    [InlineData("https://GitHub.com/Owner/Repo.git")]
    [InlineData("https://github.com/Owner/Repo/")]
    [InlineData("https://github.com/Owner/Repo?tab=x#y")]
    [InlineData("  git@github.com:Owner/Repo.git ")]
    [InlineData("http://github.com/Owner/Repo")]
    public void Canonical_Normalises(string input) =>
        Assert.Equal("https://github.com/Owner/Repo", RepoUrl.Canonical(input));

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("github.com/a/b")]
    [InlineData("ftp://github.com/a/b")]
    [InlineData("https://github.com/a")]
    [InlineData("https://github.com/a/b/tree/main")]
    [InlineData("https://user:pw@github.com/a/b")]
    [InlineData("/home/me/repos/notes.git")]
    [InlineData("file:///home/me/repos/notes.git")]
    [InlineData("https://github.com/a/%2e%2e")]
    public void Canonical_RejectsNonRepoUrls(string? input) => Assert.Null(RepoUrl.Canonical(input));

    [Fact]
    public void Canonical_KeepsNonDefaultPort() =>
        Assert.Equal("https://git.example.com:8443/a/b", RepoUrl.Canonical("https://Git.Example.com:8443/a/b.git"));
}

public class RemoteCacheLayoutTests
{
    [Fact]
    public void CanonicalUrl_MapsToHostOwnerRepo() =>
        Assert.Equal(["github.com", "Owner", "Repo"], RemoteCacheLayout.Segments("git@github.com:Owner/Repo.git", null));

    [Fact]
    public void SpellingsOfOneRepo_ShareAPath() =>
        Assert.Equal(
            RemoteCacheLayout.RelativePath("https://GitHub.com/Owner/Repo.git", null),
            RemoteCacheLayout.RelativePath("https://github.com/Owner/Repo/", null));

    [Fact]
    public void Branch_IsSuffixed_WithSlashesAsTilde() =>
        Assert.Equal("remotes/github.com/a/b@topic~soup", RemoteCacheLayout.RelativePath("https://github.com/a/b", "topic/soup"));

    [Fact]
    public void Port_IsMadeFileNameSafe() =>
        Assert.Equal("git.example.com_8443", RemoteCacheLayout.Segments("https://git.example.com:8443/a/b", null)[0]);

    [Fact]
    public void LocalPath_GoesUnderLocal_WithStableHash()
    {
        var a = RemoteCacheLayout.Segments("/srv/git/notes.git", null);
        var b = RemoteCacheLayout.Segments("/other/place/notes.git", null);

        Assert.Equal("local", a[0]);
        Assert.Matches("^notes-[0-9a-f]{8}$", a[1]);
        Assert.NotEqual(a[1], b[1]);
        Assert.Equal(a, RemoteCacheLayout.Segments("/srv/git/notes.git", null));
    }

    [Theory]
    [InlineData(@"C:\repos\my notes")]
    [InlineData("/")]
    [InlineData("..")]
    public void LocalPath_SegmentsAreSafe(string remote)
    {
        foreach (var segment in RemoteCacheLayout.Segments(remote, null))
        {
            Assert.Matches("^[A-Za-z0-9._~@-]+$", segment);
            Assert.NotEqual("..", segment);
        }
    }
}

public class RemoteCacheSettingsTests
{
    [Theory]
    [InlineData("remotes")]
    [InlineData("Remotes/wiki")]
    [InlineData(".")]
    [InlineData("./")]
    public void ContentPath_OverlappingRemoteCache_IsRejected(string path)
    {
        var result = new PublicTxt.Core.Models.TxtInstanceSettings { WikiPath = path }.Validate();
        Assert.Contains(result.Errors, e => e.StartsWith("WikiPath") && e.Contains("subscribed content"));
    }

    [Fact]
    public void ContentPath_NamedLikeRemotes_IsAllowed() =>
        Assert.True(new PublicTxt.Core.Models.TxtInstanceSettings { WikiPath = "remotes-wiki" }.Validate().IsValid);
}
