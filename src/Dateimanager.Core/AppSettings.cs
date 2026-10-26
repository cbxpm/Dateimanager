using System.Text.Json;
using System.Text.Json.Serialization;

namespace Dateimanager.Core;

public sealed class AppSettings
{
    public string? LeftPath { get; set; }
    public string? RightPath { get; set; }
    public bool ShowHidden { get; set; }
    public List<ButtonBankDefinition> Banks { get; set; } = DefaultButtons.CreateBanks();
    public int ActiveBank { get; set; }
    public AppearanceSettings Appearance { get; set; } = new();

    // Ältere Versionen kannten nur eine Knopfleiste; wird beim Laden in eine Bank übernommen
    [JsonPropertyName("buttons")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<ButtonDefinition>? LegacyButtons { get; set; }

    [JsonPropertyName("buttonColumns")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? LegacyButtonColumns { get; set; }

    internal void Normalize()
    {
        if (LegacyButtons is not null)
        {
            var defaults = DefaultButtons.CreateBanks();
            defaults[0].Buttons = LegacyButtons;
            defaults[0].Columns = LegacyButtonColumns ?? defaults[0].Columns;
            Banks = defaults;
            LegacyButtons = null;
            LegacyButtonColumns = null;
        }

        Banks = Banks?.Where(b => b is not null).ToList() ?? [];
        if (Banks.Count == 0)
            Banks = DefaultButtons.CreateBanks();

        foreach (var bank in Banks)
        {
            bank.Name = string.IsNullOrWhiteSpace(bank.Name) ? "Bank" : bank.Name;
            bank.Columns = Math.Clamp(bank.Columns, 1, 20);
            bank.Buttons ??= [];
        }

        ActiveBank = Math.Clamp(ActiveBank, 0, Banks.Count - 1);
        Appearance = (Appearance ?? new AppearanceSettings()).Normalized();
    }
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
                settings.Normalize();
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
