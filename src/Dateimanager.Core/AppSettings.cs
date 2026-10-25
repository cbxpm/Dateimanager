using System.Text.Json;
using System.Text.Json.Serialization;

namespace Dateimanager.Core;

public sealed class AppSettings
{
    public string? LeftPath { get; set; }
    public string? RightPath { get; set; }
    public bool ShowHidden { get; set; }
    public int ButtonColumns { get; set; } = 8;
    public List<ButtonDefinition> Buttons { get; set; } = DefaultButtons.Create();
}

/// <summary>Lädt und speichert die Einstellungen als JSON-Datei.</summary>
public sealed class SettingsStore(string filePath)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    public string FilePath { get; } = filePath;

    public static string DefaultFilePath() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Dateimanager", "settings.json");

    public AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath) &&
                JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath), JsonOptions) is { } settings)
            {
                settings.Buttons ??= DefaultButtons.Create();
                settings.ButtonColumns = Math.Clamp(settings.ButtonColumns, 1, 20);
                return settings;
            }
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            // Defekte Datei nicht überschreiben, sondern beiseitelegen
            TryBackupBrokenFile();
        }

        return new AppSettings();
    }

    public void Save(AppSettings settings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        var temp = FilePath + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(settings, JsonOptions));
        File.Move(temp, FilePath, overwrite: true);
    }

    private void TryBackupBrokenFile()
    {
        try
        {
            File.Copy(FilePath, FilePath + ".defekt", overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }
}
