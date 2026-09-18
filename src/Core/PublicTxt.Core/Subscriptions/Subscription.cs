using System.Text.RegularExpressions;

namespace PublicTxt.Core.Subscriptions;

/// <summary>
/// A subscription to another PublicTxt repository: where it lives, which branch to follow, and
/// which of its content to pull into the local aggregate view.
/// </summary>
public sealed partial class Subscription
{
    [GeneratedRegex(@"^[A-Za-z0-9][A-Za-z0-9._-]{0,63}$")]
    private static partial Regex NamePattern();

    /// <summary>Short unique identifier, also used as the cache directory name. Letters, digits, <c>.</c>, <c>_</c>, <c>-</c>.</summary>
    public required string Name { get; set; }

    public required string RemoteUrl { get; set; }

    /// <summary>Branch to follow; null means the remote's default branch.</summary>
    public string? Branch { get; set; }

    public SubscriptionFilter Filter { get; set; } = new();

    public DateTimeOffset? AddedAt { get; set; }

    /// <summary>When the local cache last fetched successfully.</summary>
    public DateTimeOffset? LastUpdatedAt { get; set; }

    /// <summary>Commit SHA the cache was at after the last update.</summary>
    public string? LastCommitSha { get; set; }

    public static bool IsValidName(string? name) => name is not null && NamePattern().IsMatch(name);

    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();
        if (!IsValidName(Name))
            errors.Add($"Subscription name '{Name}' must be 1-64 characters of letters, digits, '.', '_' or '-', starting with a letter or digit.");
        if (string.IsNullOrWhiteSpace(RemoteUrl))
            errors.Add($"Subscription '{Name}' has no remote URL.");
        if (Branch is not null && (Branch.Trim().Length == 0 || Branch.Contains("..") || Branch.StartsWith('-')))
            errors.Add($"Subscription '{Name}' has an invalid branch name: '{Branch}'.");
        errors.AddRange(Filter.Validate().Select(e => $"Subscription '{Name}': {e}"));
        return errors;
    }

    public override string ToString() => $"{Name} ({RemoteUrl}{(Branch is null ? "" : "@" + Branch)})";
}
