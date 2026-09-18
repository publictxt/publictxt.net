using System.CommandLine;
using PublicTxt.Git;

namespace PublicTxt.CLI;

public sealed partial class CliApp
{
    private readonly Option<string?> _messageOption = new("--message", "-m") { Description = "Commit message." };

    // ── commit ───────────────────────────────────────────────────────────────

    private Command BuildCommit()
    {
        var cmd = new Command("commit", "Stage all changes and commit.");
        cmd.Options.Add(_pathOption);
        cmd.Options.Add(_messageOption);
        cmd.Options.Add(_authorOption);
        cmd.SetAction(Guard(parse =>
        {
            var root = ResolveRoot(parse);
            var message = parse.GetValue(_messageOption);
            if (string.IsNullOrWhiteSpace(message))
                throw new CliException("A commit message is required (-m).");

            var repo = OpenRepo(root);
            if (repo.GetStatus().IsClean)
            {
                _out.WriteLine("Nothing to commit: working tree clean.");
                return ExitOk;
            }

            repo.StageAll();
            var commit = repo.Commit(message, ResolveIdentity(parse, repo));
            _out.WriteLine($"[{repo.CurrentBranch} {commit.Sha[..7]}] {commit.Message}");
            return ExitOk;
        }));
        return cmd;
    }

    // ── sync ─────────────────────────────────────────────────────────────────

    private Command BuildSync()
    {
        var remoteOption = new Option<string>("--remote") { Description = "Remote name.", DefaultValueFactory = _ => "origin" };

        var cmd = new Command("sync", "Commit local changes (with -m), pull from origin and push.");
        cmd.Options.Add(_pathOption);
        cmd.Options.Add(_messageOption);
        cmd.Options.Add(_authorOption);
        cmd.Options.Add(_tokenOption);
        cmd.Options.Add(remoteOption);
        cmd.SetAction(Guard(parse =>
        {
            var root = ResolveRoot(parse);
            var service = new TxtInstanceGitService(ResolveCredentials(parse));
            var instance = BuildInstance(root);
            var repo = service.OpenRepository(instance);
            if (!repo.IsInitialized)
                throw new CliException($"Not a git repository: {root}");

            var identity = ResolveIdentity(parse, repo);
            var message = parse.GetValue(_messageOption);
            var result = repo.Sync(identity, string.IsNullOrWhiteSpace(message) ? null : message, parse.GetValue(remoteOption)!);
            service.Refresh(instance);

            WriteKeyValues(
                ("committed", result.Committed is { } c ? $"{c.Sha[..7]} {c.Message}" : "nothing"),
                ("pulled", result.Pulled?.Status.ToString().ToLowerInvariant() ?? "no remote branch yet"),
                ("pushed", result.Pushed ? "yes" : "nothing to push"),
                ("status", instance.GitStatus.ToString()));

            if (result.HasConflicts)
            {
                _out.WriteLine();
                _out.WriteLine("Conflicts; resolve these files, then commit and sync again:");
                foreach (var f in result.Pulled!.ConflictedFiles ?? [])
                    _out.WriteLine($"  {f}");
                return ExitConflicts;
            }
            return ExitOk;
        }));
        return cmd;
    }

    // ── publish ──────────────────────────────────────────────────────────────

    private Command BuildPublish()
    {
        var branchOption = new Option<string>("--branch") { Description = "Remote branch to publish to.", DefaultValueFactory = _ => "gh-pages" };
        var remoteOption = new Option<string>("--remote") { Description = "Remote name.", DefaultValueFactory = _ => "origin" };

        var cmd = new Command("publish", "Push the current branch to a Pages-style branch on the remote.");
        cmd.Options.Add(_pathOption);
        cmd.Options.Add(branchOption);
        cmd.Options.Add(remoteOption);
        cmd.Options.Add(_tokenOption);
        cmd.SetAction(Guard(parse =>
        {
            var root = ResolveRoot(parse);
            var repo = OpenRepo(root, ResolveCredentials(parse));
            var branch = parse.GetValue(branchOption)!;
            var remote = parse.GetValue(remoteOption)!;

            if (!repo.GetStatus().IsClean)
                _out.WriteLine("warning: working tree has uncommitted changes; only committed content is published.");

            repo.PushTo(remote, branch);
            _out.WriteLine($"Published {repo.CurrentBranch} ({repo.LatestCommit?.Sha[..7]}) to {remote}/{branch}.");
            return ExitOk;
        }));
        return cmd;
    }

    private static IPrimaryRepository OpenRepo(string root, IGitCredentials? credentials = null)
    {
        var repo = new PrimaryRepository(root, credentials);
        if (!repo.IsInitialized)
            throw new CliException($"Not a git repository: {root}");
        return repo;
    }
}
