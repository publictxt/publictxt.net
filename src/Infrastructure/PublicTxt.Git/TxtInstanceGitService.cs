using PublicTxt.Core.Models;

namespace PublicTxt.Git;

/// <summary>
/// Bridges a <see cref="TxtInstance"/> with its <see cref="IPrimaryRepository"/>: creates or clones the
/// working copy, runs sync cycles, and keeps the instance's git-related fields in step with the repository.
/// </summary>
public sealed class TxtInstanceGitService(IGitCredentials? credentials = null)
{
    /// <summary>Returns a repository handle for the instance's <see cref="TxtInstance.LocalPath"/> (no I/O).</summary>
    /// <exception cref="InvalidOperationException">The instance has no local path.</exception>
    public IPrimaryRepository OpenRepository(TxtInstance instance)
    {
        ArgumentNullException.ThrowIfNull(instance);
        if (!instance.HasLocalPath())
            throw new InvalidOperationException($"Instance '{instance.Name}' has no LocalPath.");

        return new PrimaryRepository(instance.LocalPath!, credentials);
    }

    /// <summary>
    /// Ensures a git repository exists at the instance's local path. If the path is not yet a repository,
    /// it is cloned from <see cref="TxtInstance.RemoteUrl"/> when set, otherwise initialised empty.
    /// If a remote URL is set but the repository has no <c>origin</c>, one is added. Safe to call repeatedly.
    /// </summary>
    public IPrimaryRepository Initialize(TxtInstance instance)
    {
        var repo = OpenRepository(instance);
        try
        {
            if (!repo.IsInitialized)
            {
                if (instance.HasRemoteUrl())
                    repo.Clone(instance.RemoteUrl!);
                else
                    repo.Init();
            }

            if (instance.HasRemoteUrl() && repo.GetRemotes().All(r => r.Name != "origin"))
                repo.AddRemote("origin", instance.RemoteUrl!);

            Refresh(instance, repo);
            instance.Status = InstanceStatus.Ready;
            instance.UpdateLastAccessed();
            return repo;
        }
        catch
        {
            MarkError(instance);
            throw;
        }
    }

    /// <summary>
    /// Re-reads branch and sync state from disk into the instance. With <paramref name="fetch"/> the
    /// origin remote is fetched first so unpulled remote commits are detected.
    /// </summary>
    public void Refresh(TxtInstance instance, bool fetch = false)
    {
        var repo = OpenRepository(instance);
        try
        {
            if (fetch && repo.IsInitialized && repo.GetRemotes().Any(r => r.Name == "origin"))
                repo.Fetch();

            Refresh(instance, repo);
        }
        catch
        {
            MarkError(instance);
            throw;
        }
    }

    /// <summary>
    /// Runs a sync cycle (see <see cref="IPrimaryRepository.Sync"/>) and updates the instance's
    /// status, tracking state and <see cref="TxtInstance.LastGitSync"/>.
    /// </summary>
    public GitSyncResult Sync(TxtInstance instance, GitIdentity identity, string? commitMessage = null)
    {
        var repo = OpenRepository(instance);
        instance.Status = InstanceStatus.Syncing;
        instance.GitStatus = GitStatus.Syncing;
        try
        {
            var result = repo.Sync(identity, commitMessage);
            instance.LastGitSync = DateTimeOffset.UtcNow;
            Refresh(instance, repo);
            instance.Status = InstanceStatus.Ready;
            return result;
        }
        catch
        {
            MarkError(instance);
            throw;
        }
    }

    /// <summary>Derives the instance-level <see cref="GitStatus"/> from working tree and tracking state.</summary>
    public static GitStatus ComputeGitStatus(WorkingTreeStatus tree, TrackingStatus tracking)
    {
        if (tree.HasConflicts)
            return GitStatus.Conflicted;

        var localWork = !tree.IsClean || tracking.Ahead > 0;

        if (!tracking.IsTracking)
            return localWork ? GitStatus.LocalChanges : GitStatus.Unknown;

        if (localWork && tracking.Behind > 0) return GitStatus.Diverged;
        if (localWork) return GitStatus.LocalChanges;
        if (tracking.Behind > 0) return GitStatus.RemoteChanges;
        return GitStatus.Synced;
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private static void Refresh(TxtInstance instance, IPrimaryRepository repo)
    {
        if (!repo.IsInitialized)
        {
            instance.Status = InstanceStatus.Uninitialized;
            instance.GitStatus = GitStatus.Unknown;
            return;
        }

        instance.CurrentBranch = repo.CurrentBranch;
        instance.GitStatus = ComputeGitStatus(repo.GetStatus(), repo.GetTrackingStatus());
    }

    private static void MarkError(TxtInstance instance)
    {
        instance.Status = InstanceStatus.Error;
        instance.GitStatus = GitStatus.Error;
    }
}
