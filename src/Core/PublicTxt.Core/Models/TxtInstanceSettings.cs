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
}