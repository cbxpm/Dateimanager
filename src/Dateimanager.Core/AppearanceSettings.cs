using System.Text.Json.Serialization;

namespace Dateimanager.Core;

public enum RowSpacing
{
    Compact,
    Normal,
    Wide,
}

/// <summary>Schriftart und -größe; ein leerer Name steht für die Standardschrift.</summary>
public sealed record FontSetting(string Family = "", double Size = FontSetting.DefaultSize)
{
    public const double DefaultSize = 13;
    public const double MinSize = 8;
    public const double MaxSize = 32;

    public FontSetting Normalized() => this with
    {
        Family = Family?.Trim() ?? string.Empty,
        Size = double.IsFinite(Size) ? Math.Clamp(Math.Round(Size), MinSize, MaxSize) : DefaultSize,
    };
}

public sealed record AppearanceSettings
{
    public FontSetting ListFont { get; init; } = new();
    public FontSetting ButtonFont { get; init; } = new();
    public RowSpacing RowSpacing { get; init; } = RowSpacing.Compact;

    /// <summary>Zeilenhöhe der Dateilisten in Pixeln, abhängig von Schriftgröße und Zeilenabstand.</summary>
    [JsonIgnore]
    public double RowHeight => Math.Ceiling(ListFont.Size * 1.3) + RowSpacing switch
    {
        RowSpacing.Compact => 4,
        RowSpacing.Normal => 10,
        _ => 16,
    };

    public AppearanceSettings Normalized() => this with
    {
        ListFont = (ListFont ?? new FontSetting()).Normalized(),
        ButtonFont = (ButtonFont ?? new FontSetting()).Normalized(),
        RowSpacing = Enum.IsDefined(RowSpacing) ? RowSpacing : RowSpacing.Compact,
    };
}
