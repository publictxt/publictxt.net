using System.Globalization;
using System.Text.RegularExpressions;

namespace PublicTxt.Core.Content;

/// <summary>
/// The blog layout convention: <c>blog/yyyy/MM/dd/&lt;file&gt;.md</c>, where the file is either the
/// day's main post named <c>yyyyMMdd.md</c> or another post named by title.
/// </summary>
public static partial class BlogPathConvention
{
    [GeneratedRegex(@"(?:^|/)(?<y>\d{4})/(?<m>\d{2})/(?<d>\d{2})/[^/]+$")]
    private static partial Regex DatedDirectoryPattern();

    [GeneratedRegex(@"(?:^|/)(?<y>\d{4})(?<m>\d{2})(?<d>\d{2})\.md$", RegexOptions.IgnoreCase)]
    private static partial Regex DatedFileNamePattern();

    /// <summary>
    /// Extracts the post date from a path relative to the blog root or the instance root.
    /// The <c>yyyy/MM/dd</c> directory wins; a <c>yyyyMMdd.md</c> file name is the fallback.
    /// </summary>
    public static bool TryGetDate(string relativePath, out DateOnly date)
    {
        var path = relativePath.Replace('\\', '/');

        var m = DatedDirectoryPattern().Match(path);
        if (!m.Success)
            m = DatedFileNamePattern().Match(path);

        if (m.Success && TryBuild(m, out date))
            return true;

        date = default;
        return false;
    }

    /// <summary>Directory (relative to the blog root) for posts on <paramref name="date"/>: <c>yyyy/MM/dd</c>.</summary>
    public static string DirectoryFor(DateOnly date) =>
        $"{date.Year:D4}/{date.Month:D2}/{date.Day:D2}";

    /// <summary>File name for the day's main post: <c>yyyyMMdd.md</c>.</summary>
    public static string MainPostFileName(DateOnly date) =>
        $"{date.Year:D4}{date.Month:D2}{date.Day:D2}.md";

    /// <summary>Full path relative to the blog root for the day's main post.</summary>
    public static string MainPostPath(DateOnly date) =>
        $"{DirectoryFor(date)}/{MainPostFileName(date)}";

    private static bool TryBuild(Match m, out DateOnly date)
    {
        var y = int.Parse(m.Groups["y"].Value, CultureInfo.InvariantCulture);
        var mo = int.Parse(m.Groups["m"].Value, CultureInfo.InvariantCulture);
        var d = int.Parse(m.Groups["d"].Value, CultureInfo.InvariantCulture);
        return DateOnly.TryParseExact($"{y:D4}-{mo:D2}-{d:D2}", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
    }
}
