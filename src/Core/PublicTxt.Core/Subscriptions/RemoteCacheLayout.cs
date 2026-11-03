using System.Security.Cryptography;
using System.Text;

namespace PublicTxt.Core.Subscriptions;

/// <summary>
/// Where a subscribed repository's local checkout lives inside an instance:
/// <c>remotes/&lt;host&gt;/&lt;owner&gt;/&lt;repo&gt;[@branch]</c>, keyed by the canonical URL so subscriptions to
/// the same repository and branch share one checkout. The folder is a disposable, git-ignored cache.
/// </summary>
public static class RemoteCacheLayout
{
    /// <summary>Instance-relative folder holding every checkout.</summary>
    public const string DirectoryName = "remotes";

    /// <summary>Folder under <see cref="DirectoryName"/> for remotes that are not canonical repo URLs (local paths, <c>file://</c>, …).</summary>
    public const string LocalDirectoryName = "local";

    /// <summary>
    /// Path segments of the checkout, relative to <see cref="DirectoryName"/>.
    /// A followed branch is appended to the last segment as <c>@branch</c> with <c>/</c> written as <c>~</c>
    /// (which git forbids in branch names, so the mapping cannot collide); the remote's default branch has no suffix.
    /// Non-canonical remotes go under <c>local/&lt;name&gt;-&lt;hash&gt;</c>, the hash taken over the remote string.
    /// </summary>
    public static IReadOnlyList<string> Segments(string remoteUrl, string? branch)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(remoteUrl);
        var suffix = branch is null ? string.Empty : "@" + SafeName(branch.Replace('/', '~'));

        if (RepoUrl.Canonical(remoteUrl) is { } canonical)
        {
            var parts = canonical["https://".Length..].Split('/');
            return [SafeName(parts[0]), parts[1], parts[2] + suffix];
        }

        var trimmed = remoteUrl.Trim().TrimEnd('/', '\\');
        var last = trimmed[(trimmed.LastIndexOfAny(['/', '\\', ':']) + 1)..];
        if (last.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
            last = last[..^4];
        last = SafeName(last);
        if (last is "" or "." or "..")
            last = "repo";

        var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(remoteUrl.Trim())))[..8];
        return [LocalDirectoryName, $"{last}-{hash}{suffix}"];
    }

    /// <summary>Instance-relative path of the checkout, forward-slash separated.</summary>
    public static string RelativePath(string remoteUrl, string? branch) =>
        DirectoryName + "/" + string.Join('/', Segments(remoteUrl, branch));

    // Keeps names valid on every OS (no ':' etc. on Windows).
    private static string SafeName(string s)
    {
        var sb = new StringBuilder(s.Length);
        foreach (var c in s)
            sb.Append(char.IsAsciiLetterOrDigit(c) || c is '.' or '_' or '-' or '~' ? c : '_');
        return sb.ToString();
    }
}
