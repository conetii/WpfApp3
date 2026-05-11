using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using WpfApp3.Models;

namespace WpfApp3.Services;

public sealed class AppSettingsService
{
    private static readonly AppSettings DefaultSettings = new();

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        Converters =
        {
            new JsonStringEnumConverter()
        }
    };

    private readonly string _settingsPath;

    public AppSettingsService()
    {
        string directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WpfApp3");
        _settingsPath = Path.Combine(directory, "appsettings.json");
    }

    public AppSettings Load()
    {
        if (!File.Exists(_settingsPath))
        {
            return CreateDefaultSettings();
        }

        try
        {
            string json = File.ReadAllText(_settingsPath);
            AppSettings settings = JsonSerializer.Deserialize<AppSettings>(json, SerializerOptions) ?? CreateDefaultSettings();

            return Normalize(settings);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or NotSupportedException)
        {
            return CreateDefaultSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        try
        {
            string? directory = Path.GetDirectoryName(_settingsPath);

            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            string json = JsonSerializer.Serialize(Normalize(settings), SerializerOptions);
            File.WriteAllText(_settingsPath, json);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
        }
    }

    private static AppSettings Normalize(AppSettings? settings)
    {
        AppSettings normalized = settings ?? CreateDefaultSettings();

        if (string.IsNullOrWhiteSpace(normalized.Language))
        {
            normalized.Language = DefaultSettings.Language;
        }

        if (string.IsNullOrWhiteSpace(normalized.Theme))
        {
            normalized.Theme = DefaultSettings.Theme;
        }

        if (string.IsNullOrWhiteSpace(normalized.LastStoragePath) && !string.IsNullOrWhiteSpace(normalized.StorageFolderPath))
        {
            normalized.LastStorageKind = StorageKind.Folder;
            normalized.LastStoragePath = normalized.StorageFolderPath;
        }

        return normalized;
    }

    private static AppSettings CreateDefaultSettings()
    {
        return new AppSettings
        {
            Language = DefaultSettings.Language,
            Theme = DefaultSettings.Theme,
            StorageFolderPath = DefaultSettings.StorageFolderPath,
            LastStorageKind = DefaultSettings.LastStorageKind,
            LastStoragePath = DefaultSettings.LastStoragePath
        };
    }
}
