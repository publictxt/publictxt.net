using System.CommandLine;
using PublicTxt.Core.Instances;
using PublicTxt.Core.Models;
using PublicTxt.Git;

namespace PublicTxt.CLI;

/// <summary>Raised by command handlers for user-facing failures; printed as <c>error: …</c> with exit code 1.</summary>
internal sealed class CliException(string message) : Exception(message);

/// <summary>
/// The <c>publictxt</c> command-line application. Output goes to the supplied writers so tests can
/// drive it in-process; <see cref="Program"/> wires it to the console.
/// </summary>
public sealed partial class CliApp
{
    public const int ExitOk = 0;
    public const int ExitError = 1;
    public const int ExitConflicts = 2;

    private readonly TextWriter _out;
    private readonly TextWriter _err;
    private readonly Func<string, string?> _env;

    // Options shared by several commands.
    private readonly Option<string> _pathOption = new("--path", "-C")
    {
        Description = "Instance directory (default: current directory).",
        DefaultValueFactory = _ => "."
    };

    private readonly Option<string?> _tokenOption = new("--token")
    {
        Description = "HTTPS access token for the remote (default: $PUBLICTXT_GIT_TOKEN)."
    };

    private readonly Option<string?> _authorOption = new("--author")
    {
        Description = "Commit author as \"Name <email>\" (default: $PUBLICTXT_AUTHOR_NAME/EMAIL, then git config)."
    };

    public CliApp(TextWriter output, TextWriter error, Func<string, string?>? environment = null)
    {
        _out = output;
        _err = error;
        _env = environment ?? Environment.GetEnvironmentVariable;
    }

    public int Run(string[] args)
    {
        var root = new RootCommand("PublicTxt: Markdown knowledge in Git, synced and aggregated.");
        root.Subcommands.Add(BuildInit());
        root.Subcommands.Add(BuildClone());
        root.Subcommands.Add(BuildStatus());
        root.Subcommands.Add(BuildList());
        root.Subcommands.Add(BuildShow());
        root.Subcommands.Add(BuildLinks());
        root.Subcommands.Add(BuildCommit());
        root.Subcommands.Add(BuildSync());
        root.Subcommands.Add(BuildPublish());
        root.Subcommands.Add(BuildSubscriptions());

        var config = new InvocationConfiguration { Output = _out, Error = _err };
        return root.Parse(args).Invoke(config);
    }

    // ── handler plumbing ─────────────────────────────────────────────────────

    /// <summary>Wraps a handler so user-facing failures become a one-line error and exit code 1.</summary>
    private Func<ParseResult, int> Guard(Func<ParseResult, int> handler) => parse =>
    {
        try
        {
            return handler(parse);
        }
        catch (CliException ex)
        {
            _err.WriteLine($"error: {ex.Message}");
            return ExitError;
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or UnauthorizedAccessException or LibGit2Sharp.LibGit2SharpException)
        {
            _err.WriteLine($"error: {ex.Message}");
            return ExitError;
        }
    };

    private string ResolveRoot(ParseResult parse, string? explicitPath = null) =>
        Path.GetFullPath(explicitPath ?? parse.GetValue(_pathOption) ?? ".");

    private IGitCredentials ResolveCredentials(ParseResult parse)
    {
        var token = parse.GetValue(_tokenOption);
        return string.IsNullOrEmpty(token)
            ? new DelegateGitCredentials((_, user) =>
            {
                var envToken = _env(EnvironmentGitCredentials.DefaultVariableName);
                return string.IsNullOrEmpty(envToken) ? null : new UsernamePasswordCredential(user ?? "git", envToken);
            })
            : StaticGitCredentials.Token(token);
    }

    private GitIdentity ResolveIdentity(ParseResult parse, IPrimaryRepository repo)
    {
        var author = parse.GetValue(_authorOption);
        if (!string.IsNullOrWhiteSpace(author))
            return ParseAuthor(author);

        var name = _env("PUBLICTXT_AUTHOR_NAME");
        var email = _env("PUBLICTXT_AUTHOR_EMAIL");
        if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(email))
            return new GitIdentity(name, email);

        return repo.GetConfiguredIdentity()
            ?? throw new CliException("No author identity. Pass --author \"Name <email>\", set PUBLICTXT_AUTHOR_NAME/EMAIL, or configure git user.name/user.email.");
    }

    internal static GitIdentity ParseAuthor(string author)
    {
        var lt = author.IndexOf('<');
        var gt = author.LastIndexOf('>');
        if (lt <= 0 || gt < lt)
            throw new CliException($"Author must look like \"Name <email>\", got: {author}");

        var name = author[..lt].Trim();
        var email = author[(lt + 1)..gt].Trim();
        if (name.Length == 0 || email.Length == 0)
            throw new CliException($"Author must look like \"Name <email>\", got: {author}");

        return new GitIdentity(name, email);
    }

    private static TxtInstance BuildInstance(string root, string? remoteUrl = null) => new()
    {
        Name = Path.GetFileName(root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)),
        LocalPath = root,
        RemoteUrl = remoteUrl
    };

    private static InstanceContentReader OpenReader(string root)
    {
        if (!Directory.Exists(root))
            throw new CliException($"Directory does not exist: {root}");
        return InstanceContentReader.Open(root);
    }

    private static void RequireInstance(string root)
    {
        if (!InstanceLayout.IsInstance(root))
            throw new CliException($"Not a PublicTxt instance (no {InstanceLayout.SettingsFileRelativePath}): {root}. Run 'publictxt init' first.");
    }

    // ── output helpers ───────────────────────────────────────────────────────

    private void WriteKeyValues(params (string key, string? value)[] pairs)
    {
        var width = pairs.Max(p => p.key.Length);
        foreach (var (key, value) in pairs)
            _out.WriteLine($"{key.PadRight(width)}  {value ?? "-"}");
    }

    private void WriteTable(string[] headers, IReadOnlyList<string[]> rows)
    {
        if (rows.Count == 0)
        {
            _out.WriteLine("(none)");
            return;
        }

        var widths = headers.Select((h, i) => Math.Max(h.Length, rows.Max(r => r[i].Length))).ToArray();
        _out.WriteLine(string.Join("  ", headers.Select((h, i) => h.PadRight(widths[i]))));
        _out.WriteLine(string.Join("  ", widths.Select(w => new string('-', w))));
        foreach (var row in rows)
            _out.WriteLine(string.Join("  ", row.Select((c, i) => c.PadRight(widths[i]))).TrimEnd());
    }
}
