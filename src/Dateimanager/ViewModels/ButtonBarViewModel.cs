using System.Collections.ObjectModel;
using System.Windows.Input;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dateimanager.Core;
using Dateimanager.Services;

namespace Dateimanager.ViewModels;

/// <summary>
/// Die Knopfleiste am unteren Fensterrand mit mehreren umschaltbaren Knopfbänken (wie in Directory Opus).
/// </summary>
public partial class ButtonBarViewModel : ViewModelBase
{
    private readonly AppSettings _settings;
    private readonly Action _save;
    private readonly IDialogService _dialogs;
    private readonly Func<ButtonDefinition, Task> _execute;
    private readonly bool _initialized;

    public ButtonBarViewModel(AppSettings settings, Action save, IDialogService dialogs,
        Func<ButtonDefinition, Task> execute)
    {
        _settings = settings;
        _save = save;
        _dialogs = dialogs;
        _execute = execute;

        Banks = new ObservableCollection<ButtonBankViewModel>(settings.Banks.Select(CreateBank));
        if (Banks.Count == 0)
            Banks.Add(new ButtonBankViewModel(this, "Standard", 8, []));
        CurrentBank = Banks[Math.Clamp(settings.ActiveBank, 0, Banks.Count - 1)];
        _initialized = true;
    }

    public ObservableCollection<ButtonBankViewModel> Banks { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BankPosition))]
    public partial ButtonBankViewModel CurrentBank { get; set; }

    /// <summary>z. B. „1/3“ für die Anzeige im Umschalter.</summary>
    public string BankPosition => $"{Banks.IndexOf(CurrentBank) + 1}/{Banks.Count}";

    /// <summary>
    /// Zeilenzahl der größten Bank, damit die Knopfleiste beim Umschalten ihre Höhe behält.
    /// </summary>
    public int Rows => Math.Max(1, Banks.Max(b => (b.Buttons.Count + b.Columns - 1) / b.Columns));

    private bool HasSeveralBanks => Banks.Count > 1;

    partial void OnCurrentBankChanged(ButtonBankViewModel value)
    {
        foreach (var bank in Banks)
            bank.IsCurrent = bank == value;
        Persist();
    }

    [RelayCommand]
    private void NextBank() => CurrentBank = Banks[(Banks.IndexOf(CurrentBank) + 1) % Banks.Count];

    [RelayCommand]
    private void PreviousBank() => CurrentBank = Banks[(Banks.IndexOf(CurrentBank) - 1 + Banks.Count) % Banks.Count];

    public void SelectBank(ButtonBankViewModel bank) => CurrentBank = bank;

    [RelayCommand]
    private async Task AddBankAsync()
    {
        var name = await PromptBankNameAsync("Neue Knopfbank", $"Bank {Banks.Count + 1}");
        if (name is null)
            return;

        var bank = new ButtonBankViewModel(this, name, CurrentBank.Columns, []);
        Banks.Insert(Banks.IndexOf(CurrentBank) + 1, bank);
        CurrentBank = bank;
        BanksChanged();
    }

    [RelayCommand]
    private async Task RenameBankAsync()
    {
        var name = await PromptBankNameAsync("Knopfbank umbenennen", CurrentBank.Name);
        if (name is null)
            return;

        CurrentBank.Name = name;
        BanksChanged();
    }

    [RelayCommand]
    private void DuplicateBank()
    {
        var source = CurrentBank.ToDefinition();
        var copy = CreateBank(new ButtonBankDefinition
        {
            Name = $"{source.Name} (Kopie)",
            Columns = source.Columns,
            Buttons = source.Buttons,
        });
        Banks.Insert(Banks.IndexOf(CurrentBank) + 1, copy);
        CurrentBank = copy;
        BanksChanged();
    }

    [RelayCommand(CanExecute = nameof(HasSeveralBanks))]
    private async Task RemoveBankAsync()
    {
        if (Banks.Count < 2 ||
            !await _dialogs.ConfirmAsync("Knopfbank löschen",
                $"Die Knopfbank „{CurrentBank.Name}“ mit {CurrentBank.Buttons.Count} Knöpfen löschen?", "Löschen"))
            return;

        var index = Banks.IndexOf(CurrentBank);
        var removed = CurrentBank;
        CurrentBank = Banks[index == Banks.Count - 1 ? index - 1 : index + 1];
        Banks.Remove(removed);
        BanksChanged();
    }

    [RelayCommand]
    private void MoveBankLeft() => MoveBank(-1);

    [RelayCommand]
    private void MoveBankRight() => MoveBank(1);

    private void MoveBank(int offset)
    {
        var index = Banks.IndexOf(CurrentBank);
        var target = index + offset;
        if (target < 0 || target >= Banks.Count)
            return;

        Banks.Move(index, target);
        BanksChanged();
    }

    [RelayCommand]
    private void SetColumns(int columns)
    {
        CurrentBank.Columns = columns;
        Persist();
    }

    [RelayCommand]
    private async Task ResetToDefaultsAsync()
    {
        if (!await _dialogs.ConfirmAsync("Standardknöpfe",
                "Alle Knopfbänke durch die Standardbelegung ersetzen?", "Zurücksetzen"))
            return;

        Banks.Clear();
        foreach (var bank in DefaultButtons.CreateBanks())
            Banks.Add(CreateBank(bank));
        CurrentBank = Banks[0];
        BanksChanged();
    }

    /// <summary>Fügt einen neuen Knopf in die aktuelle Bank ein (hinter <paramref name="after"/> oder am Ende).</summary>
    [RelayCommand]
    public async Task AddButtonAsync(CommandButtonViewModel? after)
    {
        var bank = after?.Bank ?? CurrentBank;
        var created = await _dialogs.EditButtonAsync("Neuer Knopf",
            ButtonDefinition.BuiltIn("Neu", BuiltInCommand.Refresh, "#B4B4B4", "#000000"));
        if (created is null)
            return;

        var index = after is null ? bank.Buttons.Count : bank.Buttons.IndexOf(after) + 1;
        bank.Buttons.Insert(index, new CommandButtonViewModel(this, bank, created));
        Persist();
    }

    internal async Task EditButtonAsync(CommandButtonViewModel button)
    {
        var edited = await _dialogs.EditButtonAsync("Knopf bearbeiten", button.Definition);
        if (edited is null)
            return;

        var buttons = button.Bank.Buttons;
        buttons[buttons.IndexOf(button)] = new CommandButtonViewModel(this, button.Bank, edited);
        Persist();
    }

    internal void DuplicateButton(CommandButtonViewModel button)
    {
        var buttons = button.Bank.Buttons;
        buttons.Insert(buttons.IndexOf(button) + 1, new CommandButtonViewModel(this, button.Bank, button.Definition));
        Persist();
    }

    internal void MoveButton(CommandButtonViewModel button, int offset)
    {
        var buttons = button.Bank.Buttons;
        var index = buttons.IndexOf(button);
        var target = index + offset;
        if (index < 0 || target < 0 || target >= buttons.Count)
            return;

        buttons.Move(index, target);
        Persist();
    }

    internal void MoveButtonToBank(CommandButtonViewModel button, ButtonBankViewModel target)
    {
        if (target == button.Bank)
            return;

        button.Bank.Buttons.Remove(button);
        target.Buttons.Add(new CommandButtonViewModel(this, target, button.Definition));
        Persist();
    }

    internal async Task RemoveButtonAsync(CommandButtonViewModel button)
    {
        if (!await _dialogs.ConfirmAsync("Knopf entfernen", $"Den Knopf „{button.Label}“ entfernen?", "Entfernen"))
            return;

        button.Bank.Buttons.Remove(button);
        Persist();
    }

    internal Task ExecuteAsync(CommandButtonViewModel button) => _execute(button.Definition);

    private ButtonBankViewModel CreateBank(ButtonBankDefinition definition)
    {
        var bank = new ButtonBankViewModel(this, definition.Name, definition.Columns, []);
        foreach (var button in definition.Buttons)
            bank.Buttons.Add(new CommandButtonViewModel(this, bank, button));
        return bank;
    }

    private Task<string?> PromptBankNameAsync(string title, string initial) =>
        _dialogs.PromptAsync(title, "Name der Knopfbank:", initial,
            n => string.IsNullOrWhiteSpace(n) ? "Der Name darf nicht leer sein." : null);

    /// <summary>Anzahl, Reihenfolge oder Namen der Bänke haben sich geändert.</summary>
    private void BanksChanged()
    {
        OnPropertyChanged(nameof(BankPosition));
        RemoveBankCommand.NotifyCanExecuteChanged();
        foreach (var button in Banks.SelectMany(b => b.Buttons))
            button.NotifyMoveTargetsChanged();
        Persist();
    }

    private void Persist()
    {
        if (!_initialized)
            return;

        OnPropertyChanged(nameof(Rows));
        _settings.Banks = Banks.Select(b => b.ToDefinition()).ToList();
        _settings.ActiveBank = Banks.IndexOf(CurrentBank);
        _save();
    }
}

/// <summary>Eine Knopfbank.</summary>
public partial class ButtonBankViewModel : ViewModelBase
{
    private readonly ButtonBarViewModel _bar;

    public ButtonBankViewModel(ButtonBarViewModel bar, string name, int columns,
        IEnumerable<CommandButtonViewModel> buttons)
    {
        _bar = bar;
        Name = name;
        Columns = columns;
        Buttons = new ObservableCollection<CommandButtonViewModel>(buttons);
        Buttons.CollectionChanged += (_, _) => OnPropertyChanged(nameof(IsEmpty));
    }

    public ObservableCollection<CommandButtonViewModel> Buttons { get; }

    public bool IsEmpty => Buttons.Count == 0;

    [ObservableProperty]
    public partial string Name { get; set; }

    [ObservableProperty]
    public partial int Columns { get; set; }

    [ObservableProperty]
    public partial bool IsCurrent { get; set; }

    [RelayCommand]
    private void Select() => _bar.SelectBank(this);

    public ButtonBankDefinition ToDefinition() => new()
    {
        Name = Name,
        Columns = Columns,
        Buttons = Buttons.Select(b => b.Definition).ToList(),
    };
}

/// <summary>Eintrag im Untermenü „In Bank verschieben“.</summary>
public sealed record BankMoveTarget(string Name, ICommand MoveCommand);

public partial class CommandButtonViewModel(ButtonBarViewModel bar, ButtonBankViewModel bank, ButtonDefinition definition)
    : ViewModelBase
{
    public ButtonDefinition Definition { get; } = definition;

    public ButtonBankViewModel Bank { get; } = bank;

    public string Label => Definition.Label;

    public IBrush Background => ParseBrush(Definition.Background) ?? Brushes.LightGray;
    public IBrush Foreground => ParseBrush(Definition.Foreground) ?? Brushes.Black;

    public string ToolTip => Definition.Kind == ButtonActionKind.BuiltIn
        ? BuiltInCommands.DisplayNames[Definition.Command]
        : $"{Definition.Program} {Definition.Arguments}".Trim();

    public IReadOnlyList<BankMoveTarget> MoveTargets =>
        bar.Banks.Where(b => b != Bank)
            .Select(b => new BankMoveTarget(b.Name, new RelayCommand(() => bar.MoveButtonToBank(this, b))))
            .ToList();

    public bool HasMoveTargets => bar.Banks.Count > 1;

    internal void NotifyMoveTargetsChanged()
    {
        OnPropertyChanged(nameof(MoveTargets));
        OnPropertyChanged(nameof(HasMoveTargets));
    }

    [RelayCommand]
    private Task ExecuteAsync() => bar.ExecuteAsync(this);

    [RelayCommand]
    private Task EditAsync() => bar.EditButtonAsync(this);

    [RelayCommand]
    private Task AddAfterAsync() => bar.AddButtonAsync(this);

    [RelayCommand]
    private void Duplicate() => bar.DuplicateButton(this);

    [RelayCommand]
    private void MoveLeft() => bar.MoveButton(this, -1);

    [RelayCommand]
    private void MoveRight() => bar.MoveButton(this, 1);

    [RelayCommand]
    private Task RemoveAsync() => bar.RemoveButtonAsync(this);

    internal static IBrush? ParseBrush(string? color) =>
        Color.TryParse(color, out var parsed) ? new SolidColorBrush(parsed) : null;
}
