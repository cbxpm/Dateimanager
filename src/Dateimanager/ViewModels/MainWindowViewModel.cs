using System.ComponentModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dateimanager.Core;
using Dateimanager.Services;

namespace Dateimanager.ViewModels;

/// <summary>Zwei Dateifenster nebeneinander (wie im Total Commander) und die Knopfleiste darunter.</summary>
public partial class MainWindowViewModel : ViewModelBase, IDisposable
{
    private sealed record ClipboardContent(IReadOnlyList<string> Paths, bool IsCut);

    private readonly FileSystemService _fileSystem;
    private readonly ITrashService _trash;
    private readonly IDialogService _dialogs;
    private readonly IShellService _shell;
    private readonly SettingsStore _settingsStore;
    private readonly AppSettings _settings;
    private ClipboardContent? _clipboard;

    public MainWindowViewModel(FileSystemService fileSystem, ITrashService trash, IDialogService dialogs,
        IShellService shell, SettingsStore settingsStore, AppSettings settings, IEnumerable<Location>? locations = null)
    {
        _fileSystem = fileSystem;
        _trash = trash;
        _dialogs = dialogs;
        _shell = shell;
        _settingsStore = settingsStore;
        _settings = settings;

        var locationViewModels = (locations ?? KnownLocations.Get()).Select(l => new LocationViewModel(l)).ToList();
        Left = CreatePane("Links", locationViewModels);
        Right = CreatePane("Rechts", locationViewModels);
        ActivePane = Left;
        ShowHidden = settings.ShowHidden;

        ButtonBank = new ButtonBankViewModel(settings, SaveSettings, dialogs, ExecuteButtonAsync);
    }

    public PaneViewModel Left { get; }
    public PaneViewModel Right { get; }
    public ButtonBankViewModel ButtonBank { get; }

    [ObservableProperty]
    public partial PaneViewModel ActivePane { get; set; }

    public PaneViewModel InactivePane => ActivePane == Left ? Right : Left;

    [ObservableProperty]
    public partial bool ShowHidden { get; set; }

    [ObservableProperty]
    public partial bool IsBusy { get; private set; }

    private bool HasSelection => ActivePane.Selection.Count > 0;
    private bool HasSingleSelection => ActivePane.Selection.Count == 1;
    private bool CanPaste => _clipboard is not null;

    public Task InitializeAsync(string leftPath, string rightPath) =>
        Task.WhenAll(Left.NavigateAsync(leftPath), Right.NavigateAsync(rightPath));

    /// <summary>Speichert Knöpfe und die aktuell geöffneten Ordner.</summary>
    public void SaveSettings()
    {
        if (Left.CurrentPath.Length > 0)
            _settings.LeftPath = Left.CurrentPath;
        if (Right.CurrentPath.Length > 0)
            _settings.RightPath = Right.CurrentPath;

        try
        {
            _settingsStore.Save(_settings);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Trace.TraceWarning($"Einstellungen konnten nicht gespeichert werden: {ex.Message}");
        }
    }

    public Task ExecuteButtonAsync(ButtonDefinition button) =>
        button.Kind == ButtonActionKind.BuiltIn ? ExecuteBuiltInAsync(button.Command) : RunExternalAsync(button);

    public Task ExecuteBuiltInAsync(BuiltInCommand command)
    {
        switch (command)
        {
            case BuiltInCommand.Copy: return TransferAsync(move: false);
            case BuiltInCommand.Move: return TransferAsync(move: true);
            case BuiltInCommand.NewFolder: return NewFolderAsync();
            case BuiltInCommand.Delete: return MoveToTrashAsync();
            case BuiltInCommand.DeletePermanently: return DeletePermanentlyAsync();
            case BuiltInCommand.Rename: return RenameAsync();
            case BuiltInCommand.Open: return ActivePane.OpenSelectedAsync();
            case BuiltInCommand.Parent: return ActivePane.GoUpAsync();
            case BuiltInCommand.Root: return ActivePane.GoRootAsync();
            case BuiltInCommand.Back: return ActivePane.GoBackAsync();
            case BuiltInCommand.Forward: return ActivePane.GoForwardAsync();
            case BuiltInCommand.Refresh: return RefreshAsync();
            case BuiltInCommand.SwapPanes: return SwapPanesAsync();
            case BuiltInCommand.SyncPanes: return SyncPanesAsync();
            case BuiltInCommand.SelectAll: ActivePane.SelectAll(); break;
            case BuiltInCommand.SelectNone: ActivePane.SelectNone(); break;
            case BuiltInCommand.InvertSelection: ActivePane.InvertSelection(); break;
            case BuiltInCommand.ToggleHidden: ShowHidden = !ShowHidden; break;
        }

        return Task.CompletedTask;
    }

    private async Task RunExternalAsync(ButtonDefinition button)
    {
        var pane = ActivePane;
        var selected = pane.SelectedPaths;

        if (string.IsNullOrWhiteSpace(button.Program))
        {
            await _dialogs.ShowErrorAsync(button.Label, "Für diesen Knopf ist kein Programm eingetragen.");
            return;
        }

        if (CommandTemplate.UsesSelection(button.Arguments) && selected.Count == 0)
        {
            await _dialogs.ShowErrorAsync(button.Label, "Für diesen Befehl müssen Dateien ausgewählt sein.");
            return;
        }

        IEnumerable<IReadOnlyList<string>> runs = button.RunPerFile && selected.Count > 0
            ? selected.Select(p => (IReadOnlyList<string>)[p])
            : [selected];

        try
        {
            foreach (var paths in runs)
            {
                var context = new CommandContext(pane.CurrentPath, InactivePane.CurrentPath, paths);
                _shell.Run(button.Program, CommandTemplate.Expand(button.Arguments, context), pane.CurrentPath);
            }
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or IOException)
        {
            await _dialogs.ShowErrorAsync(button.Label, $"„{button.Program}“ konnte nicht gestartet werden:\n{ex.Message}");
        }
    }

    [RelayCommand]
    private void SwitchPane() => ActivePane = InactivePane;

    [RelayCommand]
    private Task SwapPanesAsync()
    {
        var left = Left.CurrentPath;
        var right = Right.CurrentPath;
        return Task.WhenAll(Left.NavigateAsync(right), Right.NavigateAsync(left));
    }

    [RelayCommand]
    private Task SyncPanesAsync() => InactivePane.NavigateAsync(ActivePane.CurrentPath);

    [RelayCommand]
    private void ToggleHidden() => ShowHidden = !ShowHidden;

    [RelayCommand]
    private Task RefreshAsync() => Task.WhenAll(Left.ReloadAsync(), Right.ReloadAsync());

    [RelayCommand]
    private Task GoBackAsync() => ActivePane.GoBackAsync();

    [RelayCommand]
    private Task GoForwardAsync() => ActivePane.GoForwardAsync();

    [RelayCommand]
    private Task GoUpAsync() => ActivePane.GoUpAsync();

    [RelayCommand]
    private Task OpenSelectedAsync() => ActivePane.OpenSelectedAsync();

    [RelayCommand]
    private void SelectAll() => ActivePane.SelectAll();

    [RelayCommand]
    private void SelectNone() => ActivePane.SelectNone();

    [RelayCommand]
    private void InvertSelection() => ActivePane.InvertSelection();

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private Task CopyToOtherAsync() => TransferAsync(move: false);

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private Task MoveToOtherAsync() => TransferAsync(move: true);

    private async Task TransferAsync(bool move)
    {
        var source = ActivePane;
        var paths = source.SelectedPaths;
        if (paths.Count == 0)
            return;

        var sourceDirectory = source.CurrentPath;
        var target = await _dialogs.PromptAsync(move ? "Verschieben" : "Kopieren",
            $"{Describe(source.Selection)} {(move ? "verschieben" : "kopieren")} nach:",
            InactivePane.CurrentPath,
            t => Directory.Exists(Path.GetFullPath(t, sourceDirectory)) ? null : "Der Zielordner existiert nicht.");
        if (target is null)
            return;

        var targetDirectory = Path.GetFullPath(target, sourceDirectory);
        await RunFileOperationAsync(move ? "Verschieben fehlgeschlagen" : "Kopieren fehlgeschlagen", () =>
            (move ? _fileSystem.Move(paths, targetDirectory) : _fileSystem.Copy(paths, targetDirectory)).LastOrDefault());
    }

    [RelayCommand]
    private async Task NewFolderAsync()
    {
        var directory = ActivePane.CurrentPath;
        var name = await _dialogs.PromptAsync("Neuer Ordner", "Name des neuen Ordners:",
            FileSystemService.GetUniqueName(directory, "Neuer Ordner"),
            n => ValidateNewName(directory, n, currentName: null));
        if (name is null)
            return;

        await RunFileOperationAsync("Ordner konnte nicht erstellt werden",
            () => _fileSystem.CreateDirectory(directory, name));
    }

    [RelayCommand(CanExecute = nameof(HasSingleSelection))]
    private async Task RenameAsync()
    {
        if (ActivePane.Selection is not [var entry])
            return;

        var directory = ActivePane.CurrentPath;
        var name = await _dialogs.PromptAsync("Umbenennen", $"Neuer Name für „{entry.Name}“:", entry.Name,
            n => ValidateNewName(directory, n, entry.Name));
        if (name is null || name == entry.Name)
            return;

        await RunFileOperationAsync("Umbenennen fehlgeschlagen", () => _fileSystem.Rename(entry.FullPath, name));
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task MoveToTrashAsync()
    {
        var selection = ActivePane.Selection;
        if (selection.Count == 0 ||
            !await _dialogs.ConfirmAsync("In den Papierkorb verschieben",
                $"{Describe(selection)} in den Papierkorb verschieben?", "In den Papierkorb"))
            return;

        var paths = selection.Select(e => e.FullPath).ToList();
        await RunFileOperationAsync("Löschen fehlgeschlagen", () =>
        {
            foreach (var path in paths)
                _trash.MoveToTrash(path);
            return null;
        });
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task DeletePermanentlyAsync()
    {
        var selection = ActivePane.Selection;
        if (selection.Count == 0 ||
            !await _dialogs.ConfirmAsync("Endgültig löschen",
                $"{Describe(selection)} endgültig löschen?\nDies kann nicht rückgängig gemacht werden.",
                "Endgültig löschen"))
            return;

        var paths = selection.Select(e => e.FullPath).ToList();
        await RunFileOperationAsync("Löschen fehlgeschlagen", () =>
        {
            _fileSystem.DeletePermanently(paths);
            return null;
        });
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void ClipboardCopy() => SetClipboard(isCut: false);

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void ClipboardCut() => SetClipboard(isCut: true);

    [RelayCommand(CanExecute = nameof(CanPaste))]
    private async Task PasteAsync()
    {
        if (_clipboard is not { } clipboard)
            return;

        var destination = ActivePane.CurrentPath;
        var succeeded = await RunFileOperationAsync("Einfügen fehlgeschlagen", () =>
        {
            var result = clipboard.IsCut
                ? _fileSystem.Move(clipboard.Paths, destination)
                : _fileSystem.Copy(clipboard.Paths, destination);
            return result.LastOrDefault();
        });

        // Ausgeschnittene Elemente können nur einmal eingefügt werden
        if (succeeded && clipboard.IsCut)
        {
            _clipboard = null;
            UpdateCommandStates();
        }
    }

    partial void OnActivePaneChanged(PaneViewModel value)
    {
        Left.IsActive = value == Left;
        Right.IsActive = value == Right;
        OnPropertyChanged(nameof(InactivePane));
        UpdateCommandStates();
    }

    partial void OnShowHiddenChanged(bool value)
    {
        Left.ShowHidden = value;
        Right.ShowHidden = value;
        _settings.ShowHidden = value;
    }

    private PaneViewModel CreatePane(string name, IReadOnlyList<LocationViewModel> locations)
    {
        var pane = new PaneViewModel(name, _fileSystem, _dialogs, _shell, locations);
        pane.ActivationRequested += (_, _) => ActivePane = pane;
        pane.SelectionChanged += (_, _) =>
        {
            if (pane == ActivePane)
                UpdateCommandStates();
        };
        return pane;
    }

    /// <summary>Führt eine Dateioperation im Hintergrund aus und aktualisiert danach beide Fenster.</summary>
    /// <param name="operation">Liefert optional den Pfad, der anschließend markiert werden soll.</param>
    private async Task<bool> RunFileOperationAsync(string errorTitle, Func<string?> operation)
    {
        IsBusy = true;
        string? selectPath = null;
        var succeeded = false;
        try
        {
            selectPath = await Task.Run(operation);
            succeeded = true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            await _dialogs.ShowErrorAsync(errorTitle, ex.Message);
        }
        finally
        {
            IsBusy = false;
        }

        await Task.WhenAll(Left.ReloadAsync(SelectionFor(Left, selectPath)),
            Right.ReloadAsync(SelectionFor(Right, selectPath)));
        return succeeded;
    }

    private static string? SelectionFor(PaneViewModel pane, string? path) =>
        path is not null && PaneViewModel.PathEquals(Path.GetDirectoryName(path), pane.CurrentPath) ? path : null;

    private void SetClipboard(bool isCut)
    {
        _clipboard = new ClipboardContent(ActivePane.SelectedPaths, isCut);
        UpdateCommandStates();
    }

    private static string? ValidateNewName(string directory, string name, string? currentName)
    {
        if (FileSystemService.ValidateName(name) is { } error)
            return error;
        if (name != currentName && (File.Exists(Path.Combine(directory, name)) || Directory.Exists(Path.Combine(directory, name))))
            return $"„{name}“ existiert bereits.";
        return null;
    }

    private static string Describe(IReadOnlyList<FileEntryViewModel> entries) =>
        entries is [var single] ? $"„{single.Name}“" : $"{entries.Count} Elemente";

    private void UpdateCommandStates()
    {
        CopyToOtherCommand.NotifyCanExecuteChanged();
        MoveToOtherCommand.NotifyCanExecuteChanged();
        RenameCommand.NotifyCanExecuteChanged();
        MoveToTrashCommand.NotifyCanExecuteChanged();
        DeletePermanentlyCommand.NotifyCanExecuteChanged();
        ClipboardCopyCommand.NotifyCanExecuteChanged();
        ClipboardCutCommand.NotifyCanExecuteChanged();
        PasteCommand.NotifyCanExecuteChanged();
    }

    public void Dispose()
    {
        Left.Dispose();
        Right.Dispose();
        GC.SuppressFinalize(this);
    }
}
