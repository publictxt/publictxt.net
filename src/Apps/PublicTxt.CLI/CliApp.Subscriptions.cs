using System.CommandLine;
using PublicTxt.Core.Content;
using PublicTxt.Core.Subscriptions;
using PublicTxt.Git;

namespace PublicTxt.CLI;

public sealed partial class CliApp
{
    private Command BuildSubscriptions()
    {
        var cmd = new Command("subscriptions", "Follow other PublicTxt repositories and pull selected content into a local view.");
        cmd.Aliases.Add("subs");
        cmd.Subcommands.Add(BuildSubscriptionsAdd());
        cmd.Subcommands.Add(BuildSubscriptionsRemove());
        cmd.Subcommands.Add(BuildSubscriptionsList());
        cmd.Subcommands.Add(BuildSubscriptionsUpdate());
        return cmd;
    }

    private SubscriptionService OpenSubscriptions(ParseResult parse)
    {
        var root = ResolveRoot(parse);
        RequireInstance(root);
        return new SubscriptionService(root, ResolveCredentials(parse));
    }

    // ── add ──────────────────────────────────────────────────────────────────

    private Command BuildSubscriptionsAdd()
    {
        var urlArg = new Argument<string>("url") { Description = "Remote repository URL (HTTPS or local path)." };
        var nameOption = new Option<string?>("--name", "-n") { Description = "Subscription name (default: derived from the URL)." };
        var branchOption = new Option<string?>("--branch", "-b") { Description = "Branch to follow (default: the remote's default branch)." };
        var typeOption = new Option<ContentType[]>("--type", "-t") { Description = "Content types to include (repeatable).", AllowMultipleArgumentsPerToken = true };
        var pathOption = new Option<string[]>("--include") { Description = "Path glob to include, e.g. wiki/recipes/** (repeatable).", AllowMultipleArgumentsPerToken = true };
        var excludeOption = new Option<string[]>("--exclude") { Description = "Path glob to exclude (repeatable).", AllowMultipleArgumentsPerToken = true };
        var tagOption = new Option<string[]>("--tag") { Description = "Require at least one of these tags (repeatable).", AllowMultipleArgumentsPerToken = true };
        var excludeTagOption = new Option<string[]>("--exclude-tag") { Description = "Skip items carrying any of these tags (repeatable).", AllowMultipleArgumentsPerToken = true };

        var cmd = new Command("add", "Subscribe to a repository and fetch it.");
        cmd.Arguments.Add(urlArg);
        cmd.Options.Add(_pathOption);
        cmd.Options.Add(nameOption);
        cmd.Options.Add(branchOption);
        cmd.Options.Add(typeOption);
        cmd.Options.Add(pathOption);
        cmd.Options.Add(excludeOption);
        cmd.Options.Add(tagOption);
        cmd.Options.Add(excludeTagOption);
        cmd.Options.Add(_tokenOption);
        cmd.SetAction(Guard(parse =>
        {
            var service = OpenSubscriptions(parse);
            var url = parse.GetValue(urlArg)!;
            var sub = new Subscription
            {
                Name = parse.GetValue(nameOption) ?? DeriveDirectoryName(url),
                RemoteUrl = url,
                Branch = parse.GetValue(branchOption),
                Filter = new SubscriptionFilter
                {
                    Types = [.. parse.GetValue(typeOption) ?? []],
                    Paths = [.. parse.GetValue(pathOption) ?? []],
                    ExcludePaths = [.. parse.GetValue(excludeOption) ?? []],
                    Tags = [.. parse.GetValue(tagOption) ?? []],
                    ExcludeTags = [.. parse.GetValue(excludeTagOption) ?? []]
                }
            };

            var result = service.Add(sub);
            var count = service.Enumerate(result.Subscription).Count();

            _out.WriteLine($"Subscribed to '{sub.Name}' ({url}{(sub.Branch is null ? "" : "@" + sub.Branch)}) at {result.CommitSha[..7]}.");
            WriteKeyValues(
                ("filter", sub.Filter.Describe()),
                ("items", count.ToString()),
                ("cache", service.CachePathFor(sub.Name)));
            _out.WriteLine();
            _out.WriteLine("Commit settings/subscriptions.json to keep this subscription with the instance.");
            return ExitOk;
        }));
        return cmd;
    }

    // ── remove ───────────────────────────────────────────────────────────────

    private Command BuildSubscriptionsRemove()
    {
        var nameArg = new Argument<string>("name") { Description = "Subscription name." };

        var cmd = new Command("remove", "Unsubscribe and delete the local cache.");
        cmd.Aliases.Add("rm");
        cmd.Arguments.Add(nameArg);
        cmd.Options.Add(_pathOption);
        cmd.SetAction(Guard(parse =>
        {
            var service = OpenSubscriptions(parse);
            var name = parse.GetValue(nameArg)!;
            if (!service.Remove(name))
                throw new CliException($"No subscription named '{name}'.");

            _out.WriteLine($"Removed subscription '{name}'.");
            return ExitOk;
        }));
        return cmd;
    }

    // ── list ─────────────────────────────────────────────────────────────────

    private Command BuildSubscriptionsList()
    {
        var cmd = new Command("list", "Show subscriptions and their state.");
        cmd.Aliases.Add("ls");
        cmd.Options.Add(_pathOption);
        cmd.SetAction(Guard(parse =>
        {
            var service = OpenSubscriptions(parse);
            var rows = service.List().Select(s => new[]
            {
                s.Name,
                s.RemoteUrl + (s.Branch is null ? "" : "@" + s.Branch),
                s.Filter.Describe(),
                service.IsCached(s) ? (s.LastCommitSha?[..7] ?? "?") : "not fetched",
                s.LastUpdatedAt?.ToString("yyyy-MM-dd HH:mm") ?? "never"
            }).ToList();

            WriteTable(["name", "remote", "filter", "commit", "updated"], rows);
            return ExitOk;
        }));
        return cmd;
    }

    // ── update ───────────────────────────────────────────────────────────────

    private Command BuildSubscriptionsUpdate()
    {
        var nameArg = new Argument<string?>("name")
        {
            Description = "Subscription to update (default: all).",
            Arity = ArgumentArity.ZeroOrOne
        };

        var cmd = new Command("update", "Fetch the latest content for subscriptions.");
        cmd.Arguments.Add(nameArg);
        cmd.Options.Add(_pathOption);
        cmd.Options.Add(_tokenOption);
        cmd.SetAction(Guard(parse =>
        {
            var service = OpenSubscriptions(parse);
            var results = service.Update(parse.GetValue(nameArg));
            if (results.Count == 0)
            {
                _out.WriteLine("No subscriptions.");
                return ExitOk;
            }

            var rows = results.Select(r => new[]
            {
                r.Subscription.Name,
                r.Succeeded ? (r.Changed ? "updated" : "up to date") : "failed",
                r.Succeeded ? r.CommitSha[..7] : r.Error ?? ""
            }).ToList();
            WriteTable(["name", "result", "detail"], rows);

            return results.All(r => r.Succeeded) ? ExitOk : ExitError;
        }));
        return cmd;
    }

    // ── shared: aggregate view for list/status ───────────────────────────────

    private readonly Option<bool> _allOption = new("--all", "-a")
    {
        Description = "Include content from subscriptions."
    };

    private AggregateCatalog BuildAggregate(ParseResult parse, string root)
    {
        var reader = OpenReader(root);
        if (!parse.GetValue(_allOption) || !Core.Instances.InstanceLayout.IsInstance(root))
            return AggregateCatalog.LocalOnly(reader);

        var service = new SubscriptionService(root, ResolveCredentials(parse));
        var agg = service.BuildAggregate(reader);
        foreach (var f in agg.Failures)
            _err.WriteLine($"warning: subscription '{f.Subscription.Name}' skipped: {f.Message}");
        return agg;
    }
}
