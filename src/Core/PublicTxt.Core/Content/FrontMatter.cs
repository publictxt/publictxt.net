using System.Globalization;

namespace PublicTxt.Core.Content;

/// <summary>
/// Parsed YAML front matter. Values are the raw YAML scalars/lists/maps (strings, lists of objects,
/// dictionaries); the typed accessors cover the conventions PublicTxt cares about.
/// </summary>
public sealed class FrontMatter
{
    public static FrontMatter Empty { get; } = new(new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase));

    public IReadOnlyDictionary<string, object?> Values { get; }

    public FrontMatter(IReadOnlyDictionary<string, object?> values)
    {
        Values = values;
    }

    public bool IsEmpty => Values.Count == 0;

    public bool ContainsKey(string key) => Values.ContainsKey(key);

    /// <summary>Returns the scalar string value for <paramref name="key"/>, or null if absent or not a scalar.</summary>
    public string? GetString(string key)
    {
        if (!Values.TryGetValue(key, out var v) || v is null)
            return null;

        return v switch
        {
            string s => s,
            System.Collections.IEnumerable => null,
            _ => Convert.ToString(v, CultureInfo.InvariantCulture)
        };
    }

    /// <summary>
    /// Returns a list value. Accepts a YAML list, or a single string that is split on commas
    /// (or whitespace when there are no commas) so <c>tags: a, b</c> and <c>tags: a b</c> both work.
    /// </summary>
    public IReadOnlyList<string> GetStringList(string key)
    {
        if (!Values.TryGetValue(key, out var v) || v is null)
            return Array.Empty<string>();

        return v switch
        {
            string s when s.Contains(',') => Split(s, ','),
            string s => Split(s, ' '),
            System.Collections.IEnumerable list => list.Cast<object?>()
                .Select(o => o?.ToString())
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => s!.Trim())
                .ToList(),
            _ => new[] { v.ToString()! }
        };

        static List<string> Split(string s, char sep) => s
            .Split(sep, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
    }

    /// <summary>Parses a date from a scalar value in ISO (<c>yyyy-MM-dd</c>) or any culture-invariant format.</summary>
    public DateOnly? GetDate(string key)
    {
        var s = GetString(key);
        if (s is null) return null;
        if (DateOnly.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)) return d;
        if (DateTimeOffset.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var dt))
            return DateOnly.FromDateTime(dt.UtcDateTime);
        return null;
    }
}
