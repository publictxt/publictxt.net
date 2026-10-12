using System.Text.Json;
using System.Text.Json.Serialization;
using PublicTxt.Core.Models;

namespace PublicTxt.Core.Instances;

/// <summary>Reads and writes <see cref="TxtInstanceSettings"/> as JSON at <see cref="InstanceLayout.SettingsFileRelativePath"/>.</summary>
public sealed class TxtInstanceSettingsStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never
    };

    public bool Exists(string rootPath) => InstanceLayout.IsInstance(rootPath);

    /// <summary>Loads the settings file, or returns defaults when it does not exist.</summary>
    /// <exception cref="InvalidOperationException">The file exists but is not valid JSON or fails validation.</exception>
    public TxtInstanceSettings Load(string rootPath) => Load(new PhysicalFileSource(rootPath));

    /// <inheritdoc cref="Load(string)"/>
    public TxtInstanceSettings Load(IInstanceFileSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        const string file = InstanceLayout.SettingsFileRelativePath;
        if (!source.FileExists(file))
            return TxtInstanceSettings.CreateDefault();

        TxtInstanceSettings? settings;
        try
        {
            settings = JsonSerializer.Deserialize<TxtInstanceSettings>(source.ReadAllText(file), Options);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"Settings file is not valid JSON: {file} in {source}", ex);
        }

        settings ??= TxtInstanceSettings.CreateDefault();

        var validation = settings.Validate();
        if (!validation.IsValid)
            throw new InvalidOperationException(
                $"Settings file {file} in {source} is invalid: {string.Join("; ", validation.Errors)}");

        return settings;
    }

    /// <summary>Writes the settings file, creating the directory if needed.</summary>
    /// <exception cref="InvalidOperationException">The settings fail validation.</exception>
    public void Save(string rootPath, TxtInstanceSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var validation = settings.Validate();
        if (!validation.IsValid)
            throw new InvalidOperationException("Invalid settings: " + string.Join("; ", validation.Errors));

        var file = InstanceLayout.SettingsFilePath(rootPath);
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, JsonSerializer.Serialize(settings, Options) + Environment.NewLine);
    }
}
