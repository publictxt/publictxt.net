using PublicTxt.Git;

namespace GitTests;

public class GitCredentialsTests
{
    // ── resolvers ────────────────────────────────────────────────────────────

    [Fact]
    public void StaticGitCredentials_Token_ResolvesUsernamePassword()
    {
        var creds = StaticGitCredentials.Token("secret-token", "alice");

        var resolved = creds.Resolve("https://example.com/repo.git", null);

        var up = Assert.IsType<UsernamePasswordCredential>(resolved);
        Assert.Equal("alice", up.Username);
        Assert.Equal("secret-token", up.Password);
    }

    [Fact]
    public void StaticGitCredentials_Token_DefaultsUsernameToGit()
    {
        var resolved = StaticGitCredentials.Token("t").Resolve("https://example.com/repo.git", null);

        Assert.Equal("git", Assert.IsType<UsernamePasswordCredential>(resolved).Username);
    }

    [Fact]
    public void EnvironmentGitCredentials_ResolvesNull_WhenVariableUnset()
    {
        var variable = $"PUBLICTXT_TEST_{Guid.NewGuid():N}";
        var creds = new EnvironmentGitCredentials(variable);

        Assert.Null(creds.Resolve("https://example.com/repo.git", null));
    }

    [Fact]
    public void EnvironmentGitCredentials_ResolvesToken_WhenVariableSet()
    {
        var variable = $"PUBLICTXT_TEST_{Guid.NewGuid():N}";
        Environment.SetEnvironmentVariable(variable, "env-token");
        try
        {
            var resolved = new EnvironmentGitCredentials(variable).Resolve("https://example.com/repo.git", null);

            var up = Assert.IsType<UsernamePasswordCredential>(resolved);
            Assert.Equal("git", up.Username);
            Assert.Equal("env-token", up.Password);
        }
        finally
        {
            Environment.SetEnvironmentVariable(variable, null);
        }
    }

    [Fact]
    public void EnvironmentGitCredentials_PrefersUsernameFromUrl()
    {
        var variable = $"PUBLICTXT_TEST_{Guid.NewGuid():N}";
        Environment.SetEnvironmentVariable(variable, "env-token");
        try
        {
            var resolved = new EnvironmentGitCredentials(variable).Resolve("https://bob@example.com/repo.git", "bob");

            Assert.Equal("bob", Assert.IsType<UsernamePasswordCredential>(resolved).Username);
        }
        finally
        {
            Environment.SetEnvironmentVariable(variable, null);
        }
    }

    [Fact]
    public void DelegateGitCredentials_PassesUrlAndUsername()
    {
        string? seenUrl = null, seenUser = null;
        var creds = new DelegateGitCredentials((url, user) =>
        {
            seenUrl = url;
            seenUser = user;
            return new DefaultCredential();
        });

        var resolved = creds.Resolve("https://example.com/r.git", "carol");

        Assert.IsType<DefaultCredential>(resolved);
        Assert.Equal("https://example.com/r.git", seenUrl);
        Assert.Equal("carol", seenUser);
    }

    // ── adapter to LibGit2Sharp ──────────────────────────────────────────────

    [Fact]
    public void Adapter_MapsUsernamePassword()
    {
        var native = GitCredentialAdapter.ToLibGit2(new UsernamePasswordCredential("u", "p"));

        var up = Assert.IsType<LibGit2Sharp.UsernamePasswordCredentials>(native);
        Assert.Equal("u", up.Username);
        Assert.Equal("p", up.Password);
    }

    [Fact]
    public void Adapter_MapsDefaultAndNull_ToDefaultCredentials()
    {
        Assert.IsType<LibGit2Sharp.DefaultCredentials>(GitCredentialAdapter.ToLibGit2(new DefaultCredential()));
        Assert.IsType<LibGit2Sharp.DefaultCredentials>(GitCredentialAdapter.ToLibGit2(null));
    }

    [Fact]
    public void Adapter_ReturnsNullHandler_WhenNoCredentialsConfigured()
    {
        Assert.Null(GitCredentialAdapter.ToHandler(null));
    }

    [Fact]
    public void Adapter_Handler_InvokesResolverWithUrl()
    {
        string? seenUrl = null;
        var handler = GitCredentialAdapter.ToHandler(new DelegateGitCredentials((url, _) =>
        {
            seenUrl = url;
            return new UsernamePasswordCredential("u", "p");
        }))!;

        var native = handler("https://example.com/r.git", "", LibGit2Sharp.SupportedCredentialTypes.UsernamePassword);

        Assert.Equal("https://example.com/r.git", seenUrl);
        Assert.IsType<LibGit2Sharp.UsernamePasswordCredentials>(native);
    }

    // ── integration: configured credentials do not break local remotes ──────

    [Fact]
    public void Repositories_WithCredentials_StillWorkAgainstLocalRemote()
    {
        var bare = GitTestHelpers.TempPath("cred-bare");
        var a = GitTestHelpers.TempPath("cred-a");
        var b = GitTestHelpers.TempPath("cred-b");
        try
        {
            GitTestHelpers.CreateBareRepository(bare);
            var invoked = false;
            var creds = new DelegateGitCredentials((_, _) =>
            {
                invoked = true;
                return new UsernamePasswordCredential("git", "token");
            });

            var primary = new PrimaryRepository(a, creds);
            primary.Init();
            File.WriteAllText(Path.Combine(a, "f.txt"), "x");
            primary.StageAll();
            primary.Commit("init", new GitIdentity("A", "a@example.com"));
            primary.AddRemote("origin", bare);
            primary.Push();
            primary.Fetch();
            Assert.Equal(GitMergeStatus.UpToDate, primary.Pull(new GitIdentity("A", "a@example.com")).Status);

            var external = new ExternalRepository(b, credentials: creds);
            external.CloneOrUpdate(bare);
            Assert.Equal("x", external.ReadFile("f.txt"));

            Assert.False(invoked, "local file remotes never challenge for credentials");
        }
        finally
        {
            foreach (var p in new[] { bare, a, b })
                GitTestHelpers.DeleteDirectory(p);
        }
    }
}
