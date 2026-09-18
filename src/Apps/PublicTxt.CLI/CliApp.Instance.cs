using System.CommandLine;
using PublicTxt.Core.Content;
using PublicTxt.Core.Instances;
using PublicTxt.Core.Models;
using PublicTxt.Git;

namespace PublicTxt.CLI;

public sealed partial class CliApp
{
    // ── init ─────────────────────────────────────────────────────────────────

    private Command BuildInit()
    {
        var pathArg = new Argument<string>("path")
        {
            Description = "Directory to initialise (default: current directory).",
            Arity = ArgumentArity.ZeroOrOne,
            DefaultValueFactory = _ => "."
        };
        var remoteOption = new Option<string?>("--remote") { Description = "Origin URL to add." };
        var noCommitOption = new Option<bool>("--no-commit") { Description = "Do not create the initial commit." };

        var cmd = new Command("init", "Create a PublicTxt instance skeleton and git repository.");
        cmd.Arguments.Add(pathArg);
        cmd.Options.Add(remoteOption);
        cmd.Options.Add(noCommitOption);
        cmd.Options.Add(_authorOption);
        cmd.SetAction(Guard(parse =>
        {
            var root = ResolveRoot(parse, parse.GetValue(pathArg));
            var remote = parse.GetValue(remoteOption);

            var wasInstance = InstanceLayout.IsInstance(root);
            InstanceLayout.CreateSkeleton(root);

            // Initialise locally (never clone), then attach the remote if one was given.
            var service = new TxtInstanceGitService(ResolveCredentials(parse));
            var repo = service.Initialize(BuildInstance(root));
            if (remote is not null && repo.GetRemotes().All(r => r.Name != "origin"))
                repo.AddRemote("origin", remote);

            if (!parse.GetValue(noCommitOption) && repo.LatestCommit is null && !repo.GetStatus().IsClean)
            {
                repo.StageAll();
                repo.Commit("Initialise PublicTxt instance", ResolveIdentity(parse, repo));
            }

            _out.WriteLine(wasInstance ? $"Instance already exists at {root}" : $"Initialised PublicTxt instance at {root}");
            WriteKeyValues(
                ("branch", repo.CurrentBranch),
                ("remote", remote ?? "(none)"),
                ("commit", repo.LatestCommit?.Sha[..7] ?? "(none)"));
            return ExitOk;
        }));
        return cmd;
    }

    // ── clone ────────────────────────────────────────────────────────────────

    private Command BuildClone()
    {
        var urlArg = new Argument<string>("url") { Description = "Remote repository URL." };
        var pathArg = new Argument<string?>("path")
        {
            Description = "Target directory (default: derived from the URL).",
            Arity = ArgumentArity.ZeroOrOne
        };

        var cmd = new Command("clone", "Clone an existing PublicTxt instance.");
        cmd.Arguments.Add(urlArg);
        cmd.Arguments.Add(pathArg);
        cmd.Options.Add(_tokenOption);
        cmd.SetAction(Guard(parse =>
        {
            var url = parse.GetValue(urlArg)!;
            var target = parse.GetValue(pathArg) ?? DeriveDirectoryName(url);
            var root = Path.GetFullPath(target);

            if (Directory.Exists(root) && Directory.EnumerateFileSystemEntries(root).Any())
                throw new CliException($"Target directory is not empty: {root}");

            var service = new TxtInstanceGitService(ResolveCredentials(parse));
            var instance = BuildInstance(root, url);
            var repo = service.Initialize(instance);

            _out.WriteLine($"Cloned {url} into {root}");
            if (!InstanceLayout.IsInstance(root))
                _out.WriteLine($"warning: no {InstanceLayout.SettingsFileRelativePath} found; default content paths will be used.");

            WriteKeyValues(
                ("branch", repo.CurrentBranch),
                ("commit", repo.LatestCommit?.Sha[..7] ?? "(none)"),
                ("status", instance.GitStatus.ToString()));
            return ExitOk;
        }));
        return cmd;
    }

    internal static string DeriveDirectoryName(string url)
    {
        var trimmed = url.TrimEnd('/', '\\');
        var name = trimmed[(Math.Max(trimmed.LastIndexOf('/'), trimmed.LastIndexOf('\\')) + 1)..];
        if (name.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
            name = name[..^4];
        return name.Length == 0 ? "publictxt" : name;
    }

    // ── status ───────────────────────────────────────────────────────────────

    private Command BuildStatus()
    {
        var fetchOption = new Option<bool>("--fetch") { Description = "Fetch from origin first so remote changes are detected." };

        var cmd = new Command("status", "Show instance, git and content status.");
        cmd.Options.Add(_pathOption);
        cmd.Options.Add(fetchOption);
        cmd.Options.Add(_tokenOption);
        cmd.SetAction(Guard(parse =>
        {
            var root = ResolveRoot(parse);
            var reader = OpenReader(root);
            var isInstance = InstanceLayout.IsInstance(root);

            var instance = BuildInstance(root);
            var service = new TxtInstanceGitService(ResolveCredentials(parse));
            var repo = service.OpenRepository(instance);

            _out.WriteLine($"Instance: {root}{(isInstance ? "" : "  (no settings file; using defaults)")}");
            _out.WriteLine();

            if (repo.IsInitialized)
            {
                service.Refresh(instance, fetch: parse.GetValue(fetchOption));
                var tree = repo.GetStatus();
                var tracking = repo.GetTrackingStatus();
                var origin = repo.GetRemotes().FirstOrDefault(r => r.Name == "origin");

                WriteKeyValues(
                    ("branch", repo.CurrentBranch),
                    ("origin", origin?.Url ?? "(none)"),
                    ("upstream", tracking.IsTracking ? $"{tracking.UpstreamBranch} (ahead {tracking.Ahead}, behind {tracking.Behind})" : "(none)"),
                    ("git", instance.GitStatus.ToString()),
                    ("working tree", tree.IsClean ? "clean" : $"{tree.StagedCount} staged, {tree.UnstagedCount} modified, {tree.UntrackedCount} untracked{(tree.HasConflicts ? $", {tree.ConflictedCount} conflicted" : "")}"),
                    ("last commit", repo.LatestCommit is { } c ? $"{c.Sha[..7]} {c.Message}" : "(none)"));
            }
            else
            {
                _out.WriteLine("git           not a repository (run 'publictxt init')");
            }

            _out.WriteLine();
            var catalog = reader.BuildCatalog();
            var counts = catalog.Items.GroupBy(i => i.Type).OrderBy(g => g.Key).Select(g => $"{g.Key.ToString().ToLowerInvariant()} {g.Count()}");
            WriteKeyValues(
                ("content", catalog.Items.Count == 0 ? "(none)" : string.Join(", ", counts)),
                ("tags", catalog.Tags.Count.ToString()),
                ("broken links", catalog.BrokenLinks.Count().ToString()));
            return ExitOk;
        }));
        return cmd;
    }
}
