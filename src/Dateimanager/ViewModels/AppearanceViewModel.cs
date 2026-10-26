using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using Dateimanager.Core;

namespace Dateimanager.ViewModels;

/// <summary>Aktuelles Erscheinungsbild (Schriften, Zeilenhöhe), an das sich die Ansichten binden.</summary>
public partial class AppearanceViewModel : ViewModelBase
{
    public AppearanceViewModel(AppearanceSettings settings) => Apply(settings);

    public AppearanceSettings Settings { get; private set; } = new();

    [ObservableProperty]
    public partial FontFamily ListFontFamily { get; private set; } = FontFamily.Default;

    [ObservableProperty]
    public partial double ListFontSize { get; private set; }

    [ObservableProperty]
    public partial double RowHeight { get; private set; }

    [ObservableProperty]
    public partial FontFamily ButtonFontFamily { get; private set; } = FontFamily.Default;

    [ObservableProperty]
    public partial double ButtonFontSize { get; private set; }

    public void Apply(AppearanceSettings settings)
    {
        Settings = settings.Normalized();
        ListFontFamily = ToFontFamily(Settings.ListFont.Family);
        ListFontSize = Settings.ListFont.Size;
        RowHeight = Settings.RowHeight;
        ButtonFontFamily = ToFontFamily(Settings.ButtonFont.Family);
        ButtonFontSize = Settings.ButtonFont.Size;
    }

    public static FontFamily ToFontFamily(string? name) =>
        string.IsNullOrWhiteSpace(name) ? FontFamily.Default : new FontFamily(name);
}
