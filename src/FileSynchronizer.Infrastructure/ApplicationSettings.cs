using System;
using System.IO;
using System.Text.Json;

namespace FileSynchronizer.Infrastructure;

public sealed record ApplicationSettings
{
    public int MaximumRetainedSyncResults { get; init; } = 20;
}

public sealed class ApplicationSettingsStore(string settingsPath)
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    public ApplicationSettings Load()
    {
        if (!File.Exists(settingsPath))
        {
            var defaultSettings = new ApplicationSettings();
            Save(defaultSettings);
            return defaultSettings;
        }

        var settings = JsonSerializer.Deserialize<ApplicationSettings>(
            File.ReadAllText(settingsPath),
            SerializerOptions)
            ?? throw new InvalidDataException("Application settings could not be read.");
        if (settings.MaximumRetainedSyncResults <= 0)
        {
            throw new InvalidDataException("Maximum retained sync results must be greater than zero.");
        }

        return settings;
    }

    private void Save(ApplicationSettings settings)
    {
        var directoryPath = Path.GetDirectoryName(settingsPath)
            ?? throw new InvalidOperationException("Application settings require a parent directory.");
        Directory.CreateDirectory(directoryPath);
        File.WriteAllText(settingsPath, JsonSerializer.Serialize(settings, SerializerOptions));
    }
}
