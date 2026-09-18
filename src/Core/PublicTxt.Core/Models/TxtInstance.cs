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
    public bool HasRemoteUrl() => !string.IsNullOrEmpty(RemoteUrl);
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
    /// <summary>No information yet, or the instance has no upstream to compare against.</summary>
    Unknown,
    /// <summary>Working tree clean and level with the upstream branch.</summary>
    Synced,
    /// <summary>Uncommitted or unpushed local work; upstream has nothing new.</summary>
    LocalChanges,
    /// <summary>Upstream has commits not yet pulled; no local work.</summary>
    RemoteChanges,
    /// <summary>Both local work and unpulled upstream commits.</summary>
    Diverged,
    /// <summary>A merge left conflicts in the working tree that need resolving.</summary>
    Conflicted,
    /// <summary>A sync is in progress.</summary>
    Syncing,
    /// <summary>The last git operation failed.</summary>
    Error
}