using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dateimanager.Core;
using Dateimanager.Services;

namespace Dateimanager.ViewModels;

public enum SortColumn
{
    Name,
    Size,
    Type,
    Modified,
}

public partial class MainWindowViewModel : ViewModelBase, IDisposable
{
    private sealed record ClipboardContent(IReadOnlyList<string> Paths, bool IsCut);

    private readonly FileSystemService _fileSystem;
    private readonly ITrashService _trash;
    private readonly IDialogService _dialogs;
    private readonly IFileLauncher _launcher;
    private readonly NavigationHistory _history = new();
    private readonly DispatcherTimer _refreshTimer = new() { Interval = TimeSpan.FromMilliseconds(400) };

    private IReadOnlyList<FileSystemEntry> _allEntries = [];
    private IReadOnlyList<FileEntryViewModel> _selection = [];
    private ClipboardContent? _clipboard;
    private FileSystemWatcher? _watcher;
    private CancellationTokenSource? _loadCts;

    public MainWindowViewModel(FileSystemService fileSystem, ITrashService trash, IDialogService dialogs,
        IFileLauncher launcher, IEnumerable<Location>? locations = null)
    {
        _fileSystem = fileSystem;
        _trash = trash;
        _dialogs = dialogs;
        _launcher = launcher;
        Locations = new ObservableCollection<LocationViewModel>(
            (locations ?? KnownLocations.Get()).Select(l => new LocationViewModel(l)));

        _refreshTimer.Tick += (_, _) =>
        {
            _refreshTimer.Stop();
            _ = RefreshAsync();
        };
    }

    public ObservableCollection<LocationViewModel> Locations { get; }

    [ObservableProperty]
    public partial IReadOnlyList<FileEntryViewModel> Entries { get; private set; } = [];

    [ObservableProperty]
    public partial string CurrentPath { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial string AddressText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string FilterText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool ShowHidden { get; set; }

    [ObservableProperty]
    public partial bool IsBusy { get; private set; }

    [ObservableProperty]
    public partial string StatusText { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial FileEntryViewModel? SelectedEntry { get; set; }

    [ObservableProperty]
    public partial LocationViewModel? SelectedLocation { get; set; }

    [ObservableProperty]
    public partial SortColumn SortColumn { get; private set; } = SortColumn.Name;

    [ObservableProperty]
    public partial bool SortAscending { get; private set; } = true;

    public IReadOnlyList<FileEntryViewModel> Selection => _selection;

    private bool HasSelection => _selection.Count > 0;
    private bool HasSingleSelection => _selection.Count == 1;
    private bool CanPaste => _clipboard is not null && CurrentPath.Length > 0;
    private bool CanGoBack => _history.CanGoBack;
    private bool CanGoForward => _history.CanGoForward;
    private bool CanGoUp => CurrentPath.Length > 0 && Directory.GetParent(CurrentPath) is not null;

    /// <summary>Wird von der Ansicht aufgerufen, wenn sich die Auswahl in der Liste ändert.</summary>
    public void SetSelection(IEnumerable<FileEntryViewModel> selection)
    {
        _selection = selection.ToList();
        UpdateCommandStates();
        UpdateStatus();
    }

    public async Task NavigateAsync(string path, bool recordHistory = true, string? selectPath = null)
    {
        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(ExpandHome(path.Trim()), CurrentPath.Length > 0 ? CurrentPath : Environment.CurrentDirectory);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            await _dialogs.ShowErrorAsync("Ungültiger Pfad", $"„{path}“ ist kein gültiger Pfad.");
            AddressText = CurrentPath;
            return;
        }

        if (!Directory.Exists(fullPath))
        {
            await _dialogs.ShowErrorAsync("Ordner nicht gefunden", $"Der Ordner „{fullPath}“ wurde nicht gefunden.");
            AddressText = CurrentPath;
            return;
        }

        _loadCts?.Cancel();
        var cts = _loadCts = new CancellationTokenSource();
        IsBusy = true;

        try
        {
            var showHidden = ShowHidden;
            var entries = await Task.Run(() => _fileSystem.ListDirectory(fullPath, showHidden), cts.Token);
            if (cts.IsCancellationRequested)
                return;

            if (recordHistory)
                _history.Navigate(fullPath);

            var pathChanged = !PathEquals(fullPath, CurrentPath);
            CurrentPath = fullPath;
            AddressText = fullPath;
            _allEntries = entries;

            if (pathChanged)
            {
                FilterText = string.Empty;
                StartWatching(fullPath);
            }

            ApplyView(selectPath);
        }
        catch (OperationCanceledException)
        {
            // Eine neuere Navigation hat diese abgelöst
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            if (!cts.IsCancellationRequested)
            {
                AddressText = CurrentPath;
                await _dialogs.ShowErrorAsync("Ordner kann nicht geöffnet werden", ex.Message);
            }
        }
        finally
        {
            if (_loadCts == cts)
                IsBusy = false;
            UpdateCommandStates();
        }
    }

    [RelayCommand]
    public Task RefreshAsync() =>
        CurrentPath.Length == 0
            ? Task.CompletedTask
            : NavigateAsync(CurrentPath, recordHistory: false, selectPath: SelectedEntry?.FullPath);

    [RelayCommand]
    private Task NavigateToAddressAsync() => NavigateAsync(AddressText);

    [RelayCommand]
    private void ResetAddress() => AddressText = CurrentPath;

    [RelayCommand]
    private void ToggleHidden() => ShowHidden = !ShowHidden;

    [RelayCommand(CanExecute = nameof(CanGoBack))]
    private Task GoBackAsync()
    {
        var previous = CurrentPath;
        return _history.GoBack() is { } path
            ? NavigateAsync(path, recordHistory: false, selectPath: previous)
            : Task.CompletedTask;
    }

    [RelayCommand(CanExecute = nameof(CanGoForward))]
    private Task GoForwardAsync() =>
        _history.GoForward() is { } path ? NavigateAsync(path, recordHistory: false) : Task.CompletedTask;

    [RelayCommand(CanExecute = nameof(CanGoUp))]
    private Task GoUpAsync() =>
        Directory.GetParent(CurrentPath) is { } parent
            ? NavigateAsync(parent.FullName, selectPath: CurrentPath)
            : Task.CompletedTask;

    [RelayCommand]
    private void SortBy(SortColumn column)
    {
        if (SortColumn == column)
        {
            SortAscending = !SortAscending;
        }
        else
        {
            SortColumn = column;
            SortAscending = true;
        }

        ApplyView(SelectedEntry?.FullPath);
    }

    [RelayCommand]
    public async Task OpenEntryAsync(FileEntryViewModel? entry)
    {
        if (entry is null)
            return;

        if (entry.IsDirectory)
        {
            await NavigateAsync(entry.FullPath);
            return;
        }

        try
        {
            _launcher.Open(entry.FullPath);
        }
        catch (Exception ex)
        {
            await _dialogs.ShowErrorAsync("Datei kann nicht geöffnet werden", ex.Message);
        }
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task OpenSelectedAsync()
    {
        if (_selection is [var single])
        {
            await OpenEntryAsync(single);
            return;
        }

        foreach (var file in _selection.Where(e => !e.IsDirectory).ToList())
            await OpenEntryAsync(file);
    }

    [RelayCommand]
    private async Task NewFolderAsync()
    {
        var directory = CurrentPath;
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
        if (_selection is not [var entry])
            return;

        var directory = CurrentPath;
        var name = await _dialogs.PromptAsync("Umbenennen", $"Neuer Name für „{entry.Name}“:", entry.Name,
            n => ValidateNewName(directory, n, entry.Name));
        if (name is null || name == entry.Name)
            return;

        await RunFileOperationAsync("Umbenennen fehlgeschlagen", () => _fileSystem.Rename(entry.FullPath, name));
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task MoveToTrashAsync()
    {
        var paths = _selection.Select(e => e.FullPath).ToList();
        if (!await _dialogs.ConfirmAsync("In den Papierkorb verschieben",
                $"{Describe(_selection)} in den Papierkorb verschieben?", "In den Papierkorb"))
            return;

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
        var paths = _selection.Select(e => e.FullPath).ToList();
        if (!await _dialogs.ConfirmAsync("Endgültig löschen",
                $"{Describe(_selection)} endgültig löschen?\nDies kann nicht rückgängig gemacht werden.",
                "Endgültig löschen"))
            return;

        await RunFileOperationAsync("Löschen fehlgeschlagen", () =>
        {
            _fileSystem.DeletePermanently(paths);
            return null;
        });
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void Copy() => SetClipboard(isCut: false);

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void Cut() => SetClipboard(isCut: true);

    [RelayCommand(CanExecute = nameof(CanPaste))]
    private async Task PasteAsync()
    {
        if (_clipboard is not { } clipboard)
            return;

        var destination = CurrentPath;
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
            UpdateStatus();
        }
    }

    partial void OnFilterTextChanged(string value) => ApplyView(SelectedEntry?.FullPath);

    partial void OnShowHiddenChanged(bool value) => _ = RefreshAsync();

    partial void OnCurrentPathChanged(string value) =>
        SelectedLocation = Locations.FirstOrDefault(l => PathEquals(l.Path, value));

    partial void OnSelectedLocationChanged(LocationViewModel? value)
    {
        if (value is not null && !PathEquals(value.Path, CurrentPath))
            _ = NavigateAsync(value.Path);
    }

    private void ApplyView(string? selectPath)
    {
        var filter = FilterText.Trim();
        IEnumerable<FileSystemEntry> items = _allEntries;
        if (filter.Length > 0)
            items = items.Where(e => e.Name.Contains(filter, StringComparison.CurrentCultureIgnoreCase));

        var comparer = Comparer<FileSystemEntry>.Create(CompareEntries);
        Entries = items.Order(comparer).Select(e => new FileEntryViewModel(e)).ToList();
        SelectedEntry = selectPath is null ? null : Entries.FirstOrDefault(e => PathEquals(e.FullPath, selectPath));
        UpdateStatus();
    }

    private int CompareEntries(FileSystemEntry a, FileSystemEntry b)
    {
        // Ordner stehen immer vor Dateien, unabhängig von der Sortierrichtung
        if (a.IsDirectory != b.IsDirectory)
            return a.IsDirectory ? -1 : 1;

        var byName = StringComparer.CurrentCultureIgnoreCase.Compare(a.Name, b.Name);
        var result = SortColumn switch
        {
            SortColumn.Size => Nullable.Compare(a.Size, b.Size),
            SortColumn.Type => StringComparer.CurrentCultureIgnoreCase.Compare(a.Extension, b.Extension),
            SortColumn.Modified => a.LastModified.CompareTo(b.LastModified),
            _ => byName,
        };

        if (result == 0)
            return byName;
        return SortAscending ? result : -result;
    }

    /// <summary>Führt eine Dateioperation im Hintergrund aus und aktualisiert danach die Ansicht.</summary>
    /// <param name="operation">Liefert optional den Pfad, der anschließend ausgewählt werden soll.</param>
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

        await NavigateAsync(CurrentPath, recordHistory: false, selectPath: selectPath ?? SelectedEntry?.FullPath);
        return succeeded;
    }

    private void SetClipboard(bool isCut)
    {
        _clipboard = new ClipboardContent(_selection.Select(e => e.FullPath).ToList(), isCut);
        UpdateCommandStates();
        UpdateStatus();
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

    private void UpdateStatus()
    {
        var parts = new List<string>
        {
            Entries.Count == _allEntries.Count
                ? $"{Entries.Count} Elemente"
                : $"{Entries.Count} von {_allEntries.Count} Elementen",
        };

        if (_selection.Count > 0)
        {
            var selectedSize = _selection.Where(e => !e.IsDirectory).Sum(e => e.Size);
            parts.Add(selectedSize > 0
                ? $"{_selection.Count} ausgewählt ({SizeFormatter.Format(selectedSize)})"
                : $"{_selection.Count} ausgewählt");
        }

        if (_clipboard is { } clipboard)
            parts.Add($"{clipboard.Paths.Count} in der Zwischenablage ({(clipboard.IsCut ? "Ausschneiden" : "Kopieren")})");

        StatusText = string.Join("   ·   ", parts);
    }

    private void UpdateCommandStates()
    {
        GoBackCommand.NotifyCanExecuteChanged();
        GoForwardCommand.NotifyCanExecuteChanged();
        GoUpCommand.NotifyCanExecuteChanged();
        OpenSelectedCommand.NotifyCanExecuteChanged();
        RenameCommand.NotifyCanExecuteChanged();
        MoveToTrashCommand.NotifyCanExecuteChanged();
        DeletePermanentlyCommand.NotifyCanExecuteChanged();
        CopyCommand.NotifyCanExecuteChanged();
        CutCommand.NotifyCanExecuteChanged();
        PasteCommand.NotifyCanExecuteChanged();
    }

    private void StartWatching(string path)
    {
        _watcher?.Dispose();
        _watcher = null;

        try
        {
            var watcher = new FileSystemWatcher(path)
            {
                IncludeSubdirectories = false,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName |
                               NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.Attributes,
            };
            watcher.Created += OnFileSystemChanged;
            watcher.Deleted += OnFileSystemChanged;
            watcher.Changed += OnFileSystemChanged;
            watcher.Renamed += OnFileSystemChanged;
            watcher.EnableRaisingEvents = true;
            _watcher = watcher;
        }
        catch (Exception ex) when (ex is IOException or ArgumentException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
            // Ohne Überwachung bleibt nur das manuelle Aktualisieren (F5)
        }
    }

    // Kommt auf einem Hintergrund-Thread an; mehrere Ereignisse werden zu einer Aktualisierung gebündelt
    private void OnFileSystemChanged(object? sender, FileSystemEventArgs e) =>
        Dispatcher.UIThread.Post(() =>
        {
            _refreshTimer.Stop();
            _refreshTimer.Start();
        });

    private static string ExpandHome(string path) =>
        path == "~" || path.StartsWith("~/", StringComparison.Ordinal)
            ? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + path[1..]
            : path;

    private static bool PathEquals(string? a, string? b) =>
        a is not null && b is not null &&
        string.Equals(Path.TrimEndingDirectorySeparator(a), Path.TrimEndingDirectorySeparator(b),
            OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal);

    public void Dispose()
    {
        _refreshTimer.Stop();
        _watcher?.Dispose();
        _loadCts?.Cancel();
        GC.SuppressFinalize(this);
    }
}
