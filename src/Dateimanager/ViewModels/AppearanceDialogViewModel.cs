using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dateimanager.Core;

namespace Dateimanager.ViewModels;

/// <summary>Eintrag der Schriftliste; die Schrift wird für die Darstellung in sich selbst mitgeliefert.</summary>
public sealed record FontOption(string Name, FontFamily Family);

/// <summary>Auswahl einer Schriftart und -größe für einen Bereich (Dateilisten oder Knopfleiste).</summary>
public partial class FontChoiceViewModel : ViewModelBase
{
    public const string DefaultFamilyName = "(Standardschrift)";

    private readonly IReadOnlyList<FontOption> _families;
    private string _family;

    public FontChoiceViewModel(IReadOnlyList<string> families, FontSetting setting)
    {
        IEnumerable<string> names = [DefaultFamilyName, .. families.Where(f => !string.IsNullOrWhiteSpace(f))
            .Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.CurrentCultureIgnoreCase)];
        _families = names.Select(n => new FontOption(n,
            n == DefaultFamilyName ? FontFamily.Default : AppearanceViewModel.ToFontFamily(n))).ToList();
        FilteredFamilies = _families;
        _family = string.IsNullOrWhiteSpace(setting.Family) ? DefaultFamilyName : setting.Family;
        SelectedFamily = Find(_family);
        Size = (decimal)setting.Size;
    }

    public decimal MinSize => (decimal)FontSetting.MinSize;
    public decimal MaxSize => (decimal)FontSetting.MaxSize;

    [ObservableProperty]
    public partial IReadOnlyList<FontOption> FilteredFamilies { get; private set; }

    [ObservableProperty]
    public partial string Filter { get; set; } = string.Empty;

    /// <summary>Auswahl in der Liste; wird null, wenn der Filter die gewählte Schrift ausblendet.</summary>
    [ObservableProperty]
    public partial FontOption? SelectedFamily { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PreviewSize))]
    public partial decimal? Size { get; set; }

    public FontFamily PreviewFamily => AppearanceViewModel.ToFontFamily(FamilyName);
    public double PreviewSize => (double)(Size ?? (decimal)FontSetting.DefaultSize);

    public string ChosenFamily => _family;

    private string FamilyName => _family == DefaultFamilyName ? string.Empty : _family;

    partial void OnFilterChanged(string value)
    {
        var filter = value.Trim();
        FilteredFamilies = filter.Length == 0
            ? _families
            : _families.Where(f => f.Name.Contains(filter, StringComparison.CurrentCultureIgnoreCase)).ToList();
        if (FilteredFamilies.FirstOrDefault(f => f.Name == _family) is { } chosen)
            SelectedFamily = chosen;
    }

    partial void OnSelectedFamilyChanged(FontOption? value)
    {
        if (value is null)
            return;
        _family = value.Name;
        OnPropertyChanged(nameof(PreviewFamily));
        OnPropertyChanged(nameof(ChosenFamily));
    }

    public void Reset()
    {
        Filter = string.Empty;
        SelectedFamily = _families[0];
        Size = (decimal)FontSetting.DefaultSize;
    }

    public FontSetting ToSetting() => new FontSetting(FamilyName, PreviewSize).Normalized();

    // Nicht installierte Schrift aus den Einstellungen trotzdem anzeigen
    private FontOption Find(string name) =>
        _families.FirstOrDefault(f => string.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase))
        ?? new FontOption(name, AppearanceViewModel.ToFontFamily(name));
}

/// <summary>Dialog „Schriftart und Zeilenabstand“.</summary>
public partial class AppearanceDialogViewModel : ViewModelBase
{
    public AppearanceDialogViewModel(AppearanceSettings settings, IReadOnlyList<string> families)
    {
        ListFont = new FontChoiceViewModel(families, settings.ListFont);
        ButtonFont = new FontChoiceViewModel(families, settings.ButtonFont);
        RowSpacing = settings.RowSpacing;
        ListFont.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(FontChoiceViewModel.Size))
                OnPropertyChanged(nameof(PreviewRowHeight));
        };
    }

    /// <summary>Wird mit dem Ergebnis (oder null bei Abbruch) ausgelöst.</summary>
    public event EventHandler<AppearanceSettings?>? CloseRequested;

    public FontChoiceViewModel ListFont { get; }
    public FontChoiceViewModel ButtonFont { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCompact), nameof(IsNormal), nameof(IsWide), nameof(PreviewRowHeight))]
    public partial RowSpacing RowSpacing { get; set; }

    public bool IsCompact
    {
        get => RowSpacing == RowSpacing.Compact;
        set { if (value) RowSpacing = RowSpacing.Compact; }
    }

    public bool IsNormal
    {
        get => RowSpacing == RowSpacing.Normal;
        set { if (value) RowSpacing = RowSpacing.Normal; }
    }

    public bool IsWide
    {
        get => RowSpacing == RowSpacing.Wide;
        set { if (value) RowSpacing = RowSpacing.Wide; }
    }

    public double PreviewRowHeight => BuildResult().RowHeight;

    /// <summary>Beispielzeilen für die Vorschau der Dateiliste.</summary>
    public IReadOnlyList<string> PreviewRows { get; } =
        ["Dokumente", "Projekte", "Bericht 2026.pdf", "Urlaubsfoto.jpg", "notizen.txt"];

    public AppearanceSettings BuildResult() => new AppearanceSettings
    {
        ListFont = ListFont.ToSetting(),
        ButtonFont = ButtonFont.ToSetting(),
        RowSpacing = RowSpacing,
    }.Normalized();

    [RelayCommand]
    private void ResetToDefaults()
    {
        ListFont.Reset();
        ButtonFont.Reset();
        RowSpacing = RowSpacing.Compact;
    }

    [RelayCommand]
    private void Save() => CloseRequested?.Invoke(this, BuildResult());

    [RelayCommand]
    private void Cancel() => CloseRequested?.Invoke(this, null);
}
