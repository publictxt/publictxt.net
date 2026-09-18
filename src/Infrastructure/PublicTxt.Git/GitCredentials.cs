using LibGit2Sharp;
using LibGit2Sharp.Handlers;

namespace PublicTxt.Git;

/// <summary>
/// Provider-agnostic credential resolution for git remotes. Implementations decide where
/// secrets come from (configuration, environment, OS keychain, interactive prompt, ...).
/// The repository classes only call <see cref="Resolve"/> when a remote challenges for
/// authentication, so anonymous access to public remotes never triggers it.
/// </summary>
public interface IGitCredentials
{
    /// <summary>
    /// Returns the credential to use for <paramref name="url"/>, or null to let libgit2 fall back to
    /// its defaults (which will fail for hosts that require authentication).
    /// </summary>
    /// <param name="url">The remote URL being accessed.</param>
    /// <param name="usernameFromUrl">The user name embedded in the URL, if any (e.g. <c>https://alice@host/...</c>).</param>
    GitCredential? Resolve(string url, string? usernameFromUrl);
}

/// <summary>Base type for credentials handed back from <see cref="IGitCredentials"/>.</summary>
public abstract record GitCredential;

/// <summary>
/// HTTPS basic authentication. For GitHub, GitLab, Gitea and similar hosts, put a personal
/// access token in <see cref="Password"/>; <see cref="Username"/> may be any non-empty value
/// (conventionally the account name, or <c>git</c>).
/// </summary>
public sealed record UsernamePasswordCredential(string Username, string Password) : GitCredential;

/// <summary>Lets libgit2 negotiate with the platform's default mechanism (e.g. Windows integrated auth).</summary>
public sealed record DefaultCredential : GitCredential;

/// <summary>Uses the same credential for every remote.</summary>
public sealed class StaticGitCredentials(GitCredential credential) : IGitCredentials
{
    /// <summary>Convenience for token-based HTTPS access.</summary>
    public static StaticGitCredentials Token(string token, string username = "git") =>
        new(new UsernamePasswordCredential(username, token));

    public GitCredential? Resolve(string url, string? usernameFromUrl) => credential;
}

/// <summary>
/// Reads a personal access token from an environment variable (default <c>PUBLICTXT_GIT_TOKEN</c>).
/// Resolves to null when the variable is unset or empty.
/// </summary>
public sealed class EnvironmentGitCredentials(
    string variableName = EnvironmentGitCredentials.DefaultVariableName,
    string username = "git") : IGitCredentials
{
    public const string DefaultVariableName = "PUBLICTXT_GIT_TOKEN";

    public GitCredential? Resolve(string url, string? usernameFromUrl)
    {
        var token = Environment.GetEnvironmentVariable(variableName);
        return string.IsNullOrEmpty(token)
            ? null
            : new UsernamePasswordCredential(usernameFromUrl ?? username, token);
    }
}

/// <summary>Adapts a delegate to <see cref="IGitCredentials"/>, e.g. for per-host lookups or prompts.</summary>
public sealed class DelegateGitCredentials(Func<string, string?, GitCredential?> resolver) : IGitCredentials
{
    public GitCredential? Resolve(string url, string? usernameFromUrl) => resolver(url, usernameFromUrl);
}

/// <summary>Translates <see cref="IGitCredentials"/> into LibGit2Sharp callbacks.</summary>
internal static class GitCredentialAdapter
{
    public static CredentialsHandler? ToHandler(IGitCredentials? credentials)
    {
        if (credentials is null)
            return null;

        return (url, usernameFromUrl, _) =>
            ToLibGit2(credentials.Resolve(url, string.IsNullOrEmpty(usernameFromUrl) ? null : usernameFromUrl));
    }

    public static Credentials ToLibGit2(GitCredential? credential) => credential switch
    {
        UsernamePasswordCredential up => new UsernamePasswordCredentials
        {
            Username = up.Username,
            Password = up.Password
        },
        DefaultCredential or null => new DefaultCredentials(),
        _ => throw new NotSupportedException(
            $"Credential type '{credential.GetType().Name}' is not supported by the LibGit2Sharp backend. " +
            "Note: SSH keys are not supported because the bundled libgit2 native binaries are built without libssh2; use HTTPS with a token.")
    };
}
