namespace PublicTxt.Core.Git;

public sealed class GitCloneOptions
{
    public bool Checkout { get; init; } = true;

    public string? BranchName { get; init; }

    public GitCredentials? Credentials { get; init; }
}
