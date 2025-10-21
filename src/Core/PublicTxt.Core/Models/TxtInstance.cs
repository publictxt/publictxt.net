namespace PublicTxt.Core.Models;

public class TxtInstance
{
    // Identity
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public string? LocalPath { get; set; }
    
    // Git Info
    public string? RemoteUrl { get; set; }
    public string? CurrentBranch { get; set; } = "main";
    public GitStatus GitStatus { get; set; }
    public DateTimeOffset LastGitSync { get; set; }
    
    // Metadata
    public InstanceStatus Status { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? LastAccessedAt { get; set; }

    // Configuration
    // public TxtInstanceSettings Settings { get; set; }
}



public enum InstanceStatus
{
    Uninitialized,
    Ready,
    Syncing,
    Error
}

public enum GitStatus
{
    Unknown,
    Synced,
    LocalChanges,
    RemoteChanges,
    Diverged,
    Syncing,
    Error
}