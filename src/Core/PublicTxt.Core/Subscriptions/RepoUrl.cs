using System.Text.RegularExpressions;

namespace PublicTxt.Core.Subscriptions;

/// <summary>
/// Canonical repository URLs: one spelling per repository, so the same repo is recognised however
/// it was typed. Matches the Obsidian plugin (<c>obsidian-publictxt</c>, <c>src/sources/repo-url.ts</c>).
/// </summary>
public static partial class RepoUrl
{
    [GeneratedRegex(@"^git@([^:/\s]+):(\S+)$")]
    private static partial Regex ScpPattern();

    [GeneratedRegex(@"^[A-Za-z0-9._-]+$")]
    private static partial Regex SegmentPattern();

    /// <summary>
    /// <c>https://&lt;host&gt;/&lt;owner&gt;/&lt;repo&gt;</c>, or null if <paramref name="input"/> is not a repository URL.
    /// The host is lower-cased; a trailing <c>.git</c>, trailing slash, query and fragment are dropped;
    /// owner and repo keep their case. SCP-style <c>git@host:owner/repo</c> is accepted.
    /// URLs carrying credentials are rejected.
    /// </summary>
    public static string? Canonical(string? input)
    {
        var s = (input ?? string.Empty).Trim();
        var scp = ScpPattern().Match(s);
        if (scp.Success)
            s = $"https://{scp.Groups[1].Value}/{scp.Groups[2].Value}";

        if (!Uri.TryCreate(s, UriKind.Absolute, out var uri))
            return null;
        if (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp)
            return null;
        if (uri.UserInfo.Length > 0)
            return null;

        var parts = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2)
            return null;

        var owner = parts[0];
        var repo = parts[1].EndsWith(".git", StringComparison.OrdinalIgnoreCase) ? parts[1][..^4] : parts[1];
        if (!IsSegment(owner) || !IsSegment(repo))
            return null;

        var host = uri.IsDefaultPort ? uri.Host : $"{uri.Host}:{uri.Port}";
        return $"https://{host.ToLowerInvariant()}/{owner}/{repo}";
    }

    // "." and ".." match the character class but would walk out of a cache directory.
    private static bool IsSegment(string s) => SegmentPattern().IsMatch(s) && s is not "." and not "..";
}
