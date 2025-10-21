namespace PublicTxt.Core.Models;

public class TxtInstance
{
    // Identity
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Name { get; set; }
    public string? LocalPath { get; set; }
    
    // Git Info
    public string? RemoteUrl { get; set; }
    public string? CurrentBranch { get; set; } = "main";
    public GitStatus GitStatus { get; set; } = GitStatus.Unknown;
    public DateTimeOffset LastGitSync { get; set; }
    
    // Metadata
    public InstanceStatus Status { get; set; } = InstanceStatus.Uninitialized;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? LastAccessedAt { get; set; }

    // Configuration
    public TxtInstanceSettings Settings { get; set; } = new TxtInstanceSettings();
    
    // Helper Methods
    public bool HasRemoteUrl() => string.IsNullOrEmpty(RemoteUrl);
    public bool HasLocalPath() => !string.IsNullOrEmpty(LocalPath);
    
    public void UpdateLastAccessed() 
    {
        LastAccessedAt = DateTimeOffset.UtcNow;
    }
    
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