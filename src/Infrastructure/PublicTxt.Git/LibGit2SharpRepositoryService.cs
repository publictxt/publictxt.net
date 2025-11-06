using LibGit2Sharp;
using PublicTxt.Core.Git;

namespace PublicTxt.Git;

public sealed class LibGit2SharpRepositoryService : IGitRepositoryService
{
    public Task<bool> RepositoryExistsAsync(string path, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);

        return Task.FromResult(Repository.IsValid(path));
    }

    public Task CloneAsync(string sourceUrl, string workdirPath, GitCloneOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(sourceUrl);
        ArgumentException.ThrowIfNullOrEmpty(workdirPath);

        return Task.Run(() =>
        {
            var cloneOptions = BuildCloneOptions(options);
            Repository.Clone(sourceUrl, workdirPath, cloneOptions);
        }, cancellationToken);
    }

    public Task<string?> GetCurrentBranchAsync(string path, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);

        return Task.Run(() =>
        {
            using var repository = new Repository(path);
            return repository.Head?.FriendlyName;
        }, cancellationToken);
    }

    public Task FetchAsync(string path, string remoteName = "origin", CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        ArgumentException.ThrowIfNullOrEmpty(remoteName);

        return Task.Run(() =>
        {
            using var repository = new Repository(path);
            var remote = repository.Network.Remotes[remoteName] ?? throw new InvalidOperationException($"Remote '{remoteName}' not found.");

            var fetchOptions = new FetchOptions();
            if (repository.Network.Remotes.Any() && remote.FetchRefSpecs.Any())
            {
                Commands.Fetch(repository, remote.Name, remote.FetchRefSpecs.Select(s => s.Specification), fetchOptions, null);
            }
            else
            {
                Commands.Fetch(repository, remote.Name, Array.Empty<string>(), fetchOptions, null);
            }
        }, cancellationToken);
    }

    private static CloneOptions BuildCloneOptions(GitCloneOptions? options)
    {
        var cloneOptions = new CloneOptions
        {
            Checkout = options?.Checkout ?? true
        };

        if (!string.IsNullOrWhiteSpace(options?.BranchName))
        {
            cloneOptions.BranchName = options!.BranchName;
        }

        if (options?.Credentials is not null)
        {
            cloneOptions.CredentialsProvider = (_, _, _) => new UsernamePasswordCredentials
            {
                Username = options.Credentials.Username,
                Password = options.Credentials.Password
            };
        }

        return cloneOptions;
    }
}
