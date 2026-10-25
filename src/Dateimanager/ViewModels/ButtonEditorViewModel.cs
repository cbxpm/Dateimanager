using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dateimanager.Core;

namespace Dateimanager.ViewModels;

public sealed record CommandOption(BuiltInCommand Command, string Name);

/// <summary>Bearbeitet einen Knopf der Knopfleiste.</summary>
public partial class ButtonEditorViewModel : ViewModelBase
{
    public ButtonEditorViewModel(ButtonDefinition button)
    {
        Label = button.Label;
        IsExternal = button.Kind == ButtonActionKind.External;
        SelectedCommand = Commands.First(c => c.Command == button.Command);
        Program = button.Program;
        Arguments = button.Arguments;
        RunPerFile = button.RunPerFile;
        BackgroundColor = button.Background;
        ForegroundColor = button.Foreground;
    }

    /// <summary>Wird mit dem Ergebnis (oder null bei Abbruch) ausgelöst.</summary>
    public event EventHandler<ButtonDefinition?>? CloseRequested;

    public IReadOnlyList<CommandOption> Commands { get; } =
        BuiltInCommands.DisplayNames.Select(p => new CommandOption(p.Key, p.Value)).ToList();

    /// <summary>Farbpalette im Stil der Amiga-Workbench.</summary>
    public IReadOnlyList<string> Palette { get; } =
    [
        "#000000", "#FFFFFF", "#AAAAAA", "#B4B4B4", "#666666", "#3B5BA5", "#6688BB",
        "#A53B3B", "#DD5544", "#3F7F4F", "#66AA55", "#E0A040", "#FFCC33", "#8855AA",
    ];

    [ObservableProperty]
    public partial string Label { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsBuiltIn))]
    public partial bool IsExternal { get; set; }

    public bool IsBuiltIn
    {
        get => !IsExternal;
        set => IsExternal = !value;
    }

    [ObservableProperty]
    public partial CommandOption SelectedCommand { get; set; }

    [ObservableProperty]
    public partial string Program { get; set; }

    [ObservableProperty]
    public partial string Arguments { get; set; }

    [ObservableProperty]
    public partial bool RunPerFile { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PreviewBackground))]
    public partial string BackgroundColor { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PreviewForeground))]
    public partial string ForegroundColor { get; set; }

    [ObservableProperty]
    public partial string? Error { get; private set; }

    public IBrush? PreviewBackground => CommandButtonViewModel.ParseBrush(BackgroundColor);
    public IBrush? PreviewForeground => CommandButtonViewModel.ParseBrush(ForegroundColor);

    [RelayCommand]
    private void SetBackground(string color) => BackgroundColor = color;

    [RelayCommand]
    private void SetForeground(string color) => ForegroundColor = color;

    [RelayCommand]
    private void Cancel() => CloseRequested?.Invoke(this, null);

    [RelayCommand]
    private void Save()
    {
        if (BuildResult() is { } result)
            CloseRequested?.Invoke(this, result);
    }

    public ButtonDefinition? BuildResult()
    {
        Error = Validate();
        if (Error is not null)
            return null;

        return new ButtonDefinition
        {
            Label = Label.Trim(),
            Kind = IsExternal ? ButtonActionKind.External : ButtonActionKind.BuiltIn,
            Command = SelectedCommand.Command,
            Program = IsExternal ? Program.Trim() : string.Empty,
            Arguments = IsExternal ? Arguments.Trim() : string.Empty,
            RunPerFile = IsExternal && RunPerFile,
            Background = BackgroundColor.Trim(),
            Foreground = ForegroundColor.Trim(),
        };
    }

    private string? Validate()
    {
        if (string.IsNullOrWhiteSpace(Label))
            return "Bitte eine Beschriftung eingeben.";
        if (IsExternal && string.IsNullOrWhiteSpace(Program))
            return "Bitte ein Programm angeben.";
        if (!Color.TryParse(BackgroundColor.Trim(), out _))
            return "Die Hintergrundfarbe ist ungültig (Beispiel: #3B5BA5).";
        if (!Color.TryParse(ForegroundColor.Trim(), out _))
            return "Die Textfarbe ist ungültig (Beispiel: #FFFFFF).";
        return null;
    }
}
