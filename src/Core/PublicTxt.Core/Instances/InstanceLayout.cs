using PublicTxt.Core.Content;
using PublicTxt.Core.Models;

namespace PublicTxt.Core.Instances;

/// <summary>
/// On-disk conventions for a PublicTxt instance: where the settings file lives, which directory
/// holds which content type, and how to create the initial skeleton.
/// </summary>
public static class InstanceLayout
{
    /// <summary>
    /// The settings file is always at this fixed location so it can be found before settings are
    /// known. <see cref="TxtInstanceSettings.SettingsPath"/> describes where <em>other</em> settings
    /// files live and defaults to the same directory.
    /// </summary>
    public const string SettingsFileRelativePath = "settings/instance.json";

    public static string SettingsFilePath(string rootPath) =>
        Path.Combine(rootPath, "settings", "instance.json");

    /// <summary>True when <paramref name="rootPath"/> contains a settings file.</summary>
    public static bool IsInstance(string rootPath) =>
        !string.IsNullOrWhiteSpace(rootPath) && File.Exists(SettingsFilePath(rootPath));

    /// <summary>Content type → normalised relative directory (forward slashes, no trailing slash).</summary>
    public static IReadOnlyDictionary<ContentType, string> ContentPaths(TxtInstanceSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return new Dictionary<ContentType, string>
        {
            [ContentType.Blog] = Normalise(settings.BlogPath),
            [ContentType.Wiki] = Normalise(settings.WikiPath),
            [ContentType.Notes] = Normalise(settings.NotesPath),
            [ContentType.Bookmarks] = Normalise(settings.BookmarksPath),
            [ContentType.Community] = Normalise(settings.CommunityPath),
            [ContentType.Media] = Normalise(settings.MediaPath),
            [ContentType.Tags] = Normalise(settings.TagsPath),
            [ContentType.Indexes] = Normalise(settings.IndexesPath),
            [ContentType.Settings] = Normalise(settings.SettingsPath)
        };
    }

    /// <summary>Relative directory for a content type, or null for <see cref="ContentType.Unknown"/>.</summary>
    public static string? PathFor(TxtInstanceSettings settings, ContentType type) =>
        ContentPaths(settings).TryGetValue(type, out var p) ? p : null;

    /// <summary>Determines which content type an instance-relative path belongs to (longest prefix wins).</summary>
    public static ContentType TypeOf(TxtInstanceSettings settings, string relativePath)
    {
        var path = Normalise(relativePath);
        var best = ContentType.Unknown;
        var bestLength = -1;

        foreach (var (type, dir) in ContentPaths(settings))
        {
            if (dir.Length <= bestLength) continue;
            if (path.Equals(dir, StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith(dir + "/", StringComparison.OrdinalIgnoreCase))
            {
                best = type;
                bestLength = dir.Length;
            }
        }
        return best;
    }

    /// <summary>
    /// Creates the directory skeleton (each content directory with a <c>.gitkeep</c>) and writes the
    /// settings file if it does not exist. Existing files are never overwritten. Returns the settings in effect.
    /// </summary>
    public static TxtInstanceSettings CreateSkeleton(string rootPath, TxtInstanceSettings? settings = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);

        var store = new TxtInstanceSettingsStore();
        var effective = store.Exists(rootPath)
            ? store.Load(rootPath)
            : settings ?? TxtInstanceSettings.CreateDefault();

        var validation = effective.Validate();
        if (!validation.IsValid)
            throw new InvalidOperationException("Invalid instance settings: " + string.Join("; ", validation.Errors));

        Directory.CreateDirectory(rootPath);
        if (!store.Exists(rootPath))
            store.Save(rootPath, effective);

        foreach (var dir in ContentPaths(effective).Values.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var full = Path.Combine(rootPath, dir.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(full);
            if (!Directory.EnumerateFileSystemEntries(full).Any())
                File.WriteAllText(Path.Combine(full, ".gitkeep"), string.Empty);
        }

        return effective;
    }

    /// <summary>Problems that make the directory unusable as an instance. Missing content directories are not problems.</summary>
    public static IReadOnlyList<string> Validate(string rootPath, TxtInstanceSettings settings)
    {
        var issues = new List<string>();
        if (string.IsNullOrWhiteSpace(rootPath) || !Directory.Exists(rootPath))
            issues.Add($"Root directory does not exist: {rootPath}");

        var result = settings.Validate();
        issues.AddRange(result.Errors);
        return issues;
    }

    /// <summary>Forward slashes, no leading/trailing slashes, no <c>./</c> prefix.</summary>
    public static string Normalise(string relativePath)
    {
        var p = (relativePath ?? string.Empty).Replace('\\', '/').Trim();
        while (p.StartsWith("./", StringComparison.Ordinal)) p = p[2..];
        return p.Trim('/');
    }
}
