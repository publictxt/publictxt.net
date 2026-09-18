using System.CommandLine;
using PublicTxt.Core.Content;
using PublicTxt.Core.Instances;

namespace PublicTxt.CLI;

public sealed partial class CliApp
{
    // ── list ─────────────────────────────────────────────────────────────────

    private Command BuildList()
    {
        var typeOption = new Option<ContentType?>("--type", "-t") { Description = "Only this content type (blog, wiki, notes, …)." };
        var tagOption = new Option<string?>("--tag") { Description = "Only items carrying this tag." };

        var searchOption = new Option<string?>("--search", "-s") { Description = "Only items whose title, tags or body contain this text." };
        var timelineOption = new Option<bool>("--timeline") { Description = "Order newest first by date instead of by path." };

        var cmd = new Command("list", "List content items (add --all to include subscriptions).");
        cmd.Options.Add(_pathOption);
        cmd.Options.Add(typeOption);
        cmd.Options.Add(tagOption);
        cmd.Options.Add(searchOption);
        cmd.Options.Add(_allOption);
        cmd.Options.Add(timelineOption);
        cmd.Options.Add(_tokenOption);
        cmd.SetAction(Guard(parse =>
        {
            var agg = BuildAggregate(parse, ResolveRoot(parse));
            var all = parse.GetValue(_allOption);
            var type = parse.GetValue(typeOption);
            var tag = parse.GetValue(tagOption);
            var search = parse.GetValue(searchOption);

            IEnumerable<ContentItem> items = parse.GetValue(timelineOption) ? agg.Timeline() : agg.Items
                .OrderBy(i => i.Source ?? "", StringComparer.OrdinalIgnoreCase)
                .ThenBy(i => i.Type)
                .ThenBy(i => i.RelativePath, StringComparer.Ordinal);
            if (type is { } t) items = items.Where(i => i.Type == t);
            if (!string.IsNullOrEmpty(tag)) items = items.Where(i => i.HasTag(tag));
            if (!string.IsNullOrEmpty(search)) items = items.Intersect(agg.Search(search));

            var rows = items.Select(i =>
            {
                var cells = new List<string>();
                if (all) cells.Add(i.Source ?? "(local)");
                cells.AddRange([
                    i.Type.ToString().ToLowerInvariant(),
                    i.RelativePath,
                    i.Title,
                    i.Date?.ToString("yyyy-MM-dd") ?? "",
                    string.Join(", ", i.Tags)
                ]);
                return cells.ToArray();
            }).ToList();

            string[] headers = all
                ? ["source", "type", "path", "title", "date", "tags"]
                : ["type", "path", "title", "date", "tags"];
            WriteTable(headers, rows);
            return ExitOk;
        }));
        return cmd;
    }

    // ── show ─────────────────────────────────────────────────────────────────

    private Command BuildShow()
    {
        var fileArg = new Argument<string>("file") { Description = "Instance-relative path of a Markdown file." };
        var bodyOption = new Option<bool>("--body") { Description = "Also print the Markdown body." };

        var cmd = new Command("show", "Show one item: metadata, tags, links and backlinks.");
        cmd.Arguments.Add(fileArg);
        cmd.Options.Add(_pathOption);
        cmd.Options.Add(bodyOption);
        cmd.SetAction(Guard(parse =>
        {
            var reader = OpenReader(ResolveRoot(parse));
            var file = InstanceLayout.Normalise(parse.GetValue(fileArg)!);
            var item = reader.Read(file) ?? throw new CliException($"No Markdown item at: {file}");
            var catalog = reader.BuildCatalog();

            WriteKeyValues(
                ("path", item.RelativePath),
                ("type", item.Type.ToString().ToLowerInvariant()),
                ("title", item.Title),
                ("date", item.Date?.ToString("yyyy-MM-dd")),
                ("modified", item.ModifiedAt?.ToString("u")),
                ("tags", item.Tags.Count == 0 ? null : string.Join(", ", item.Tags)));

            if (!item.FrontMatter.IsEmpty)
            {
                _out.WriteLine();
                _out.WriteLine("front matter:");
                foreach (var (key, value) in item.FrontMatter.Values)
                    _out.WriteLine($"  {key}: {FormatValue(value)}");
            }

            var links = catalog.LinksFrom(item.RelativePath).ToList();
            var external = item.Links.Where(l => l.Kind == ContentLinkKind.External).ToList();
            if (links.Count > 0 || external.Count > 0)
            {
                _out.WriteLine();
                _out.WriteLine("links:");
                foreach (var l in links)
                    _out.WriteLine($"  {(l.IsBroken ? "BROKEN " : "")}{l.Link.Target} -> {l.ResolvedPath ?? "(outside instance)"}");
                foreach (var l in external)
                    _out.WriteLine($"  {l.Target}");
            }

            var backlinks = catalog.Backlinks(item.RelativePath).ToList();
            if (backlinks.Count > 0)
            {
                _out.WriteLine();
                _out.WriteLine("backlinks:");
                foreach (var b in backlinks)
                    _out.WriteLine($"  {b.RelativePath}");
            }

            if (parse.GetValue(bodyOption))
            {
                _out.WriteLine();
                _out.WriteLine(item.Body.TrimEnd());
            }
            return ExitOk;
        }));
        return cmd;
    }

    private static string FormatValue(object? value) => value switch
    {
        null => "",
        string s => s,
        System.Collections.IEnumerable list => "[" + string.Join(", ", list.Cast<object?>().Select(FormatValue)) + "]",
        _ => value.ToString() ?? ""
    };

    // ── links ────────────────────────────────────────────────────────────────

    private Command BuildLinks()
    {
        var allOption = new Option<bool>("--all") { Description = "List every internal link, not just broken ones." };

        var cmd = new Command("links", "Report broken internal links (or all links with --all).");
        cmd.Options.Add(_pathOption);
        cmd.Options.Add(allOption);
        cmd.SetAction(Guard(parse =>
        {
            var catalog = OpenReader(ResolveRoot(parse)).BuildCatalog();
            var all = parse.GetValue(allOption);
            var links = (all ? catalog.Links : catalog.BrokenLinks).ToList();

            var rows = links.Select(l => new[]
            {
                l.IsBroken ? "broken" : "ok",
                l.Source.RelativePath,
                l.Link.Target,
                l.ResolvedPath ?? "(outside instance)"
            }).ToList();

            WriteTable(["state", "source", "target", "resolved"], rows);
            _out.WriteLine();
            _out.WriteLine($"{catalog.BrokenLinks.Count()} broken of {catalog.Links.Count} internal links.");
            return catalog.BrokenLinks.Any() && !all ? ExitError : ExitOk;
        }));
        return cmd;
    }
}
