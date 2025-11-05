namespace PublicTxt.Core.Models;

public class TxtInstanceSettings
{
    // Content Type Paths (relative to LocalPath)
    public string BlogPath { get; set; } = "blog";
    public string WikiPath { get; set; } = "wiki";
    public string NotesPath { get; set; } = "notes";
    public string MetaWebPath { get; set; } = "metaweb";
    public string MediaPath { get; set; } = "media";
    public string TagsPath { get; set; } = "tags";
    public string IndexesPath { get; set; } = "indexes";
    public string CommunityPath { get; set; } = "community";
    public string SettingsPath { get; set; } = "settings";
    
    // Sync Configuration
    public bool AutoSync { get; set; } = false;
    public int SyncIntervalMinutes { get; set; } = 15;
    
    // Content Preferences
    public string DefaultBranch { get; set; } = "main";
    
    // Future:
    // public Dictionary<string, string> CustomContentPaths { get; set; }
    // public SchemaDeclaration? Schema { get; set; }
    
       
    // Validation
    public ValidationResult Validate()
    {
        var errors = new List<string>();
        
        // Validate all content paths
        var paths = GetAllContentPaths();
        foreach (var (name, path) in paths)
        {
            if (!IsValidRelativePath(path))
                errors.Add($"{name} is not a valid relative path: {path}");
        }
        
        // Check for duplicates
        var duplicates = paths.GroupBy(p => p.path)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key);
        
        foreach (var dup in duplicates)
            errors.Add($"Duplicate path found: {dup}");
        
        return new ValidationResult(errors);
    }
    
    private static bool IsValidRelativePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;
            
        if (Path.IsPathRooted(path))
            return false;
            
        if (path.Contains(".."))
            return false;
            
        // Check for invalid characters
        var invalidChars = Path.GetInvalidPathChars();
        if (path.Any(c => invalidChars.Contains(c)))
            return false;
            
        return true;
    }
    
    private IEnumerable<(string name, string path)> GetAllContentPaths()
    {
        yield return (nameof(BlogPath), BlogPath);
        yield return (nameof(WikiPath), WikiPath);
        yield return (nameof(NotesPath), NotesPath);
        yield return (nameof(MetaWebPath), MetaWebPath);
        yield return (nameof(MediaPath), MediaPath);
        yield return (nameof(TagsPath), TagsPath);
        yield return (nameof(IndexesPath), IndexesPath);
        yield return (nameof(CommunityPath), CommunityPath);
        yield return (nameof(SettingsPath), SettingsPath);
    }
    
    // Factory
    public static TxtInstanceSettings CreateDefault() => new();
}

public class ValidationResult
{
    public bool IsValid => !Errors.Any();
    public IReadOnlyList<string> Errors { get; }
    
    public ValidationResult(List<string> errors)
    {
        Errors = errors.AsReadOnly();
    }
}
