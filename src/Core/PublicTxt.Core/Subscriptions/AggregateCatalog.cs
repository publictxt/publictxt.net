using PublicTxt.Core.Content;
using PublicTxt.Core.Instances;

namespace PublicTxt.Core.Subscriptions;

/// <summary>
/// Supplies the parsed, already-filtered content of one subscription. Implemented in the Git layer over
/// a cached clone; Core only needs the items.
/// </summary>
public interface ISubscriptionContentSource
{
    /// <summary>Items from the subscription that pass its filter. Items are attributed with <see cref="ContentItem.Source"/> = subscription name.</summary>
    IEnumerable<ContentItem> Enumerate(Subscription subscription);
}

/// <summary>A subscription whose content could not be read; the aggregate continues without it.</summary>
public sealed record SubscriptionFailure(Subscription Subscription, string Message);

/// <summary>
/// The local instance's items plus the filtered items of every subscription, in one queryable view.
/// Local and subscribed items can share a relative path; <see cref="ContentItem.Source"/> tells them apart.
/// </summary>
public sealed class AggregateCatalog
{
    public IReadOnlyList<ContentItem> Items { get; }
    public IReadOnlyList<SubscriptionFailure> Failures { get; }

    /// <summary>Distinct tags across all items, sorted.</summary>
    public IReadOnlyList<string> Tags { get; }

    private AggregateCatalog(IReadOnlyList<ContentItem> items, IReadOnlyList<SubscriptionFailure> failures)
    {
        Items = items;
        Failures = failures;
        Tags = items.SelectMany(i => i.Tags)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(t => t, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public IEnumerable<ContentItem> Local => Items.Where(i => i.IsLocal);

    public IEnumerable<ContentItem> From(string subscriptionName) =>
        Items.Where(i => string.Equals(i.Source, subscriptionName, StringComparison.OrdinalIgnoreCase));

    public IEnumerable<ContentItem> OfType(ContentType type) => Items.Where(i => i.Type == type);

    public IEnumerable<ContentItem> WithTag(string tag) => Items.Where(i => i.HasTag(tag));

    /// <summary>Items sorted newest first by date, then modified time; undated last.</summary>
    public IEnumerable<ContentItem> Timeline() => Items
        .OrderByDescending(i => i.Date.HasValue)
        .ThenByDescending(i => i.Date)
        .ThenByDescending(i => i.ModifiedAt)
        .ThenBy(i => i.Source ?? string.Empty, StringComparer.OrdinalIgnoreCase)
        .ThenBy(i => i.RelativePath, StringComparer.Ordinal);

    /// <summary>Case-insensitive search over title, tags and body.</summary>
    public IEnumerable<ContentItem> Search(string term)
    {
        if (string.IsNullOrWhiteSpace(term)) return Items;
        return Items.Where(i =>
            i.Title.Contains(term, StringComparison.OrdinalIgnoreCase) ||
            i.Tags.Any(t => t.Contains(term, StringComparison.OrdinalIgnoreCase)) ||
            i.Body.Contains(term, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Builds the aggregate from the local instance and each subscription. A subscription that throws
    /// is recorded in <see cref="Failures"/> rather than failing the whole view.
    /// </summary>
    public static AggregateCatalog Build(
        InstanceContentReader local,
        IEnumerable<Subscription> subscriptions,
        ISubscriptionContentSource source)
    {
        ArgumentNullException.ThrowIfNull(local);
        ArgumentNullException.ThrowIfNull(subscriptions);
        ArgumentNullException.ThrowIfNull(source);

        var items = local.EnumerateAll().ToList();
        var failures = new List<SubscriptionFailure>();

        foreach (var sub in subscriptions)
        {
            try
            {
                foreach (var item in source.Enumerate(sub))
                    items.Add(item.Source == sub.Name ? item : item.WithSource(sub.Name));
            }
            catch (Exception ex) when (ex is IOException or InvalidOperationException or UnauthorizedAccessException)
            {
                failures.Add(new SubscriptionFailure(sub, ex.Message));
            }
        }

        return new AggregateCatalog(items, failures);
    }

    /// <summary>Local-only aggregate (no subscriptions), useful as a fallback.</summary>
    public static AggregateCatalog LocalOnly(InstanceContentReader local) =>
        new(local.EnumerateAll().ToList(), []);
}
