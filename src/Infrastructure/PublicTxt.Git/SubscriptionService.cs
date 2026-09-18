using PublicTxt.Core.Content;
using PublicTxt.Core.Instances;
using PublicTxt.Core.Models;
using PublicTxt.Core.Subscriptions;

namespace PublicTxt.Git;

/// <summary>Outcome of updating one subscription's cache.</summary>
public sealed record SubscriptionUpdateResult(Subscription Subscription, string CommitSha, bool Changed, string? Error = null)
{
    public bool Succeeded => Error is null;
}

/// <summary>
/// Manages the local caches behind an instance's subscriptions: one read-only clone per subscription
/// under <c>&lt;instance&gt;/.publictxt/subscriptions/&lt;name&gt;</c>, git-ignored so the aggregate view is
/// computed from caches rather than committed into the instance. Also serves as the
/// <see cref="ISubscriptionContentSource"/> for <see cref="AggregateCatalog"/>.
/// </summary>
public sealed class SubscriptionService : ISubscriptionContentSource
{
    public const string CacheRelativeDirectory = ".publictxt/subscriptions";
    private const string IgnoreEntry = ".publictxt/";

    private readonly string _rootPath;
    private readonly IGitCredentials? _credentials;
    private readonly SubscriptionStore _store = new();
    private readonly MarkdownContentParser _parser;

    public SubscriptionService(string instanceRootPath, IGitCredentials? credentials = null, MarkdownContentParser? parser = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceRootPath);
        _rootPath = Path.GetFullPath(instanceRootPath);
        _credentials = credentials;
        _parser = parser ?? MarkdownContentParser.Default;
    }

    public string CacheDirectory => Path.Combine(_rootPath, ".publictxt", "subscriptions");

    public string CachePathFor(string subscriptionName) => Path.Combine(CacheDirectory, subscriptionName);

    // ── list / add / remove ──────────────────────────────────────────────────

    public IReadOnlyList<Subscription> List() => _store.Load(_rootPath);

    public Subscription? Find(string name) =>
        List().FirstOrDefault(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase));

    /// <summary>Adds a subscription and updates its cache.</summary>
    /// <exception cref="InvalidOperationException">The subscription is invalid, the name is taken, or the clone fails.</exception>
    public SubscriptionUpdateResult Add(Subscription subscription)
    {
        ArgumentNullException.ThrowIfNull(subscription);
        var errors = subscription.Validate();
        if (errors.Count > 0)
            throw new InvalidOperationException(string.Join("; ", errors));

        var existing = List().ToList();
        if (existing.Any(s => string.Equals(s.Name, subscription.Name, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException($"A subscription named '{subscription.Name}' already exists.");

        subscription.AddedAt ??= DateTimeOffset.UtcNow;

        // Clone first so a failing remote does not leave a dangling entry.
        var sha = UpdateCache(subscription);
        subscription.LastUpdatedAt = DateTimeOffset.UtcNow;
        subscription.LastCommitSha = sha;

        existing.Add(subscription);
        _store.Save(_rootPath, existing);
        EnsureIgnored();
        return new SubscriptionUpdateResult(subscription, sha, Changed: true);
    }

    /// <summary>Removes the subscription and deletes its cache. Returns false if no such subscription.</summary>
    public bool Remove(string name)
    {
        var existing = List().ToList();
        var removed = existing.RemoveAll(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase));
        if (removed == 0)
            return false;

        _store.Save(_rootPath, existing);
        DeleteCache(name);
        return true;
    }

    // ── update ───────────────────────────────────────────────────────────────

    /// <summary>Fetches every subscription (or the named one). Failures are reported per subscription, not thrown.</summary>
    public IReadOnlyList<SubscriptionUpdateResult> Update(string? name = null)
    {
        var subs = List().ToList();
        var targets = name is null
            ? subs
            : subs.Where(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase)).ToList();

        if (name is not null && targets.Count == 0)
            throw new InvalidOperationException($"No subscription named '{name}'.");

        var results = new List<SubscriptionUpdateResult>();
        foreach (var sub in targets)
        {
            try
            {
                var before = sub.LastCommitSha;
                var sha = UpdateCache(sub);
                sub.LastUpdatedAt = DateTimeOffset.UtcNow;
                sub.LastCommitSha = sha;
                results.Add(new SubscriptionUpdateResult(sub, sha, Changed: before != sha));
            }
            catch (Exception ex) when (ex is InvalidOperationException or IOException or LibGit2Sharp.LibGit2SharpException)
            {
                results.Add(new SubscriptionUpdateResult(sub, sub.LastCommitSha ?? string.Empty, Changed: false, ex.Message));
            }
        }

        _store.Save(_rootPath, subs);
        EnsureIgnored();
        return results;
    }

    /// <summary>True when the subscription's cache has been cloned.</summary>
    public bool IsCached(Subscription subscription) =>
        new ExternalRepository(CachePathFor(subscription.Name)).IsInitialized;

    // ── content ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Parsed items from the subscription's cache that pass its filter. Reads the cache as it is; call
    /// <see cref="Update"/> to refresh. Throws if the cache has never been cloned.
    /// </summary>
    public IEnumerable<ContentItem> Enumerate(Subscription subscription)
    {
        ArgumentNullException.ThrowIfNull(subscription);
        var cache = CachePathFor(subscription.Name);
        if (!new ExternalRepository(cache).IsInitialized)
            throw new InvalidOperationException($"Subscription '{subscription.Name}' has not been fetched yet; run update.");

        var settings = new TxtInstanceSettingsStore().Load(cache);
        var reader = new InstanceContentReader(cache, settings, _parser);
        var filter = subscription.Filter;

        foreach (var type in InstanceLayout.ContentPaths(settings).Keys.Where(t => t.IsMarkdownContent()))
        {
            if (filter.Types.Count > 0 && !filter.Types.Contains(type))
                continue;

            foreach (var path in reader.EnumerateFiles(type))
            {
                if (!filter.MatchesPath(path, type)) continue;
                var item = reader.Read(path);
                if (item is null || !filter.Matches(item)) continue;
                yield return item.WithSource(subscription.Name);
            }
        }
    }

    /// <summary>Builds the aggregate view of the local instance plus all subscriptions.</summary>
    public AggregateCatalog BuildAggregate(InstanceContentReader local) =>
        AggregateCatalog.Build(local, List(), this);

    // ── helpers ──────────────────────────────────────────────────────────────

    private string UpdateCache(Subscription sub)
    {
        var repo = new ExternalRepository(CachePathFor(sub.Name), sub.RemoteUrl, _credentials);
        return repo.UpdateToBranch(sub.RemoteUrl, sub.Branch);
    }

    private void DeleteCache(string name)
    {
        var path = CachePathFor(name);
        if (!Directory.Exists(path)) return;

        // Git object files are read-only on Windows; clear attributes before deleting.
        foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
            File.SetAttributes(file, FileAttributes.Normal);
        Directory.Delete(path, recursive: true);
    }

    /// <summary>Adds <c>.publictxt/</c> to the instance's <c>.gitignore</c> if it is not already ignored.</summary>
    private void EnsureIgnored()
    {
        var ignore = Path.Combine(_rootPath, ".gitignore");
        var lines = File.Exists(ignore) ? File.ReadAllLines(ignore) : [];
        if (lines.Any(l => l.Trim() is IgnoreEntry or ".publictxt"))
            return;

        var content = lines.Length == 0
            ? $"# PublicTxt subscription caches{Environment.NewLine}{IgnoreEntry}{Environment.NewLine}"
            : string.Join(Environment.NewLine, lines).TrimEnd() + $"{Environment.NewLine}{Environment.NewLine}# PublicTxt subscription caches{Environment.NewLine}{IgnoreEntry}{Environment.NewLine}";
        File.WriteAllText(ignore, content);
    }
}
