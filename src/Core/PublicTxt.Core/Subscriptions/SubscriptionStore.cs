using System.Text.Json;
using System.Text.Json.Serialization;

namespace PublicTxt.Core.Subscriptions;

/// <summary>
/// Reads and writes the instance's subscription list as JSON at <see cref="RelativePath"/>,
/// so subscriptions travel with the instance in Git.
/// </summary>
public sealed class SubscriptionStore
{
    public const string RelativePath = "settings/subscriptions.json";

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    private sealed class Document
    {
        public List<Subscription> Subscriptions { get; set; } = [];
    }

    public static string FilePath(string rootPath) =>
        Path.Combine(rootPath, "settings", "subscriptions.json");

    public bool Exists(string rootPath) => File.Exists(FilePath(rootPath));

    /// <summary>Loads subscriptions, or an empty list when the file does not exist.</summary>
    /// <exception cref="InvalidOperationException">The file is not valid JSON, a subscription is invalid, or names repeat.</exception>
    public IReadOnlyList<Subscription> Load(string rootPath)
    {
        var file = FilePath(rootPath);
        if (!File.Exists(file))
            return [];

        Document? doc;
        try
        {
            doc = JsonSerializer.Deserialize<Document>(File.ReadAllText(file), Options);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"Subscriptions file is not valid JSON: {file}", ex);
        }

        var subs = doc?.Subscriptions ?? [];
        Validate(subs, file);
        return subs;
    }

    /// <exception cref="InvalidOperationException">A subscription is invalid or names repeat.</exception>
    public void Save(string rootPath, IEnumerable<Subscription> subscriptions)
    {
        ArgumentNullException.ThrowIfNull(subscriptions);
        var list = subscriptions.ToList();
        var file = FilePath(rootPath);
        Validate(list, file);

        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        var doc = new Document { Subscriptions = list.OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase).ToList() };
        File.WriteAllText(file, JsonSerializer.Serialize(doc, Options) + Environment.NewLine);
    }

    private static void Validate(IReadOnlyList<Subscription> subs, string file)
    {
        var errors = subs.SelectMany(s => s.Validate()).ToList();

        var duplicates = subs.GroupBy(s => s.Name, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key);
        errors.AddRange(duplicates.Select(d => $"Duplicate subscription name: {d}"));

        if (errors.Count > 0)
            throw new InvalidOperationException($"Invalid subscriptions in {file}: {string.Join("; ", errors)}");
    }
}
