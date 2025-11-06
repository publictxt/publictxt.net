namespace PublicTxt.Core.Git;

public interface IGitRepositoryService
{
    Task<bool> RepositoryExistsAsync(string path, CancellationToken cancellationToken = default);

    Task CloneAsync(string sourceUrl, string workdirPath, GitCloneOptions? options = null, CancellationToken cancellationToken = default);

    Task<string?> GetCurrentBranchAsync(string path, CancellationToken cancellationToken = default);

    Task FetchAsync(string path, string remoteName = "origin", CancellationToken cancellationToken = default);
}
