using System.Collections.ObjectModel;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dateimanager.Core;
using Dateimanager.Services;

namespace Dateimanager.ViewModels;

/// <summary>Die konfigurierbare Knopfleiste am unteren Fensterrand.</summary>
public partial class ButtonBankViewModel : ViewModelBase
{
    private readonly AppSettings _settings;
    private readonly Action _save;
    private readonly IDialogService _dialogs;
    private readonly Func<ButtonDefinition, Task> _execute;
    private readonly bool _initialized;

    public ButtonBankViewModel(AppSettings settings, Action save, IDialogService dialogs,
        Func<ButtonDefinition, Task> execute)
    {
        _settings = settings;
        _save = save;
        _dialogs = dialogs;
        _execute = execute;
        Buttons = new ObservableCollection<CommandButtonViewModel>(settings.Buttons.Select(Wrap));
        Columns = settings.ButtonColumns;
        _initialized = true;
    }

    public ObservableCollection<CommandButtonViewModel> Buttons { get; }

    [ObservableProperty]
    public partial int Columns { get; set; }

    public static IReadOnlyList<int> ColumnChoices { get; } = [4, 5, 6, 8, 10, 12];

    partial void OnColumnsChanged(int value) => Persist();

    [RelayCommand]
    private void SetColumns(int columns) => Columns = columns;

    [RelayCommand]
    public async Task AddAsync(CommandButtonViewModel? after)
    {
        var created = await _dialogs.EditButtonAsync("Neuer Knopf",
            ButtonDefinition.BuiltIn("Neu", BuiltInCommand.Refresh, "#B4B4B4", "#000000"));
        if (created is null)
            return;

        var index = after is null ? Buttons.Count : Buttons.IndexOf(after) + 1;
        Buttons.Insert(index, Wrap(created));
        Persist();
    }

    public async Task EditAsync(CommandButtonViewModel button)
    {
        var edited = await _dialogs.EditButtonAsync("Knopf bearbeiten", button.Definition);
        if (edited is null)
            return;

        Buttons[Buttons.IndexOf(button)] = Wrap(edited);
        Persist();
    }

    public void Duplicate(CommandButtonViewModel button)
    {
        Buttons.Insert(Buttons.IndexOf(button) + 1, Wrap(button.Definition));
        Persist();
    }

    public void Move(CommandButtonViewModel button, int offset)
    {
        var index = Buttons.IndexOf(button);
        var target = index + offset;
        if (index < 0 || target < 0 || target >= Buttons.Count)
            return;

        Buttons.Move(index, target);
        Persist();
    }

    public async Task RemoveAsync(CommandButtonViewModel button)
    {
        if (!await _dialogs.ConfirmAsync("Knopf entfernen", $"Den Knopf „{button.Label}“ entfernen?", "Entfernen"))
            return;

        Buttons.Remove(button);
        Persist();
    }

    [RelayCommand]
    private async Task ResetToDefaultsAsync()
    {
        if (!await _dialogs.ConfirmAsync("Standardknöpfe",
                "Alle Knöpfe durch die Standardbelegung ersetzen?", "Zurücksetzen"))
            return;

        Buttons.Clear();
        foreach (var definition in DefaultButtons.Create())
            Buttons.Add(Wrap(definition));
        Columns = 8;
        Persist();
    }

    internal Task ExecuteAsync(CommandButtonViewModel button) => _execute(button.Definition);

    private CommandButtonViewModel Wrap(ButtonDefinition definition) => new(this, definition);

    private void Persist()
    {
        if (!_initialized)
            return;

        _settings.Buttons = Buttons.Select(b => b.Definition).ToList();
        _settings.ButtonColumns = Columns;
        _save();
    }
}

public partial class CommandButtonViewModel(ButtonBankViewModel bank, ButtonDefinition definition) : ViewModelBase
{
    public ButtonDefinition Definition { get; } = definition;

    public string Label => Definition.Label;

    public IBrush? Background => ParseBrush(Definition.Background);
    public IBrush? Foreground => ParseBrush(Definition.Foreground);

    public string ToolTip => Definition.Kind == ButtonActionKind.BuiltIn
        ? BuiltInCommands.DisplayNames[Definition.Command]
        : $"{Definition.Program} {Definition.Arguments}".Trim();

    [RelayCommand]
    private Task ExecuteAsync() => bank.ExecuteAsync(this);

    [RelayCommand]
    private Task EditAsync() => bank.EditAsync(this);

    [RelayCommand]
    private Task AddAfterAsync() => bank.AddAsync(this);

    [RelayCommand]
    private void Duplicate() => bank.Duplicate(this);

    [RelayCommand]
    private void MoveLeft() => bank.Move(this, -1);

    [RelayCommand]
    private void MoveRight() => bank.Move(this, 1);

    [RelayCommand]
    private Task RemoveAsync() => bank.RemoveAsync(this);

    internal static IBrush? ParseBrush(string? color) =>
        Color.TryParse(color, out var parsed) ? new SolidColorBrush(parsed) : null;
}
