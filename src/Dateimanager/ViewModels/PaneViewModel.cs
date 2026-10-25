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

/// <summary>Eines der beiden Dateifenster: Ordnerinhalt, Verlauf, Sortierung und Auswahl.</summary>
public partial class PaneViewModel : ViewModelBase, IDisposable
{
    private readonly FileSystemService _fileSystem;
    private readonly IDialogService _dialogs;
    private readonly IShellService _shell;
    private readonly NavigationHistory _history = new();
    private readonly DispatcherTimer _refreshTimer = new() { Interval = TimeSpan.FromMilliseconds(400) };

    private IReadOnlyList<FileSystemEntry> _allEntries = [];
    private IReadOnlyList<FileEntryViewModel> _selection = [];
    private FileSystemWatcher? _watcher;
    private CancellationTokenSource? _loadCts;
    private string _freeSpaceText = string.Empty;
    private bool _syncingLocation;

    public PaneViewModel(string name, FileSystemService fileSystem, IDialogService dialogs, IShellService shell,
        IReadOnlyList<LocationViewModel> locations)
    {
        Name = name;
        _fileSystem = fileSystem;
        _dialogs = dialogs;
        _shell = shell;
        Locations = locations;

        _refreshTimer.Tick += (_, _) =>
        {
            _refreshTimer.Stop();
            _ = ReloadAsync();
        };
    }

    /// <summary>Die Ansicht wählt daraufhin genau diese Einträge in der Liste aus.</summary>
    public event EventHandler<IReadOnlyList<FileEntryViewModel>>? SelectionRequested;

    public event EventHandler? SelectionChanged;

    /// <summary>Das Fenster wurde angeklickt oder hat den Fokus erhalten.</summary>
    public event EventHandler? ActivationRequested;

    public string Name { get; }

    public IReadOnlyList<LocationViewModel> Locations { get; }

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
    public partial bool IsActive { get; set; }

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

    public IReadOnlyList<string> SelectedPaths => _selection.Select(e => e.FullPath).ToList();

    public bool CanGoBack => _history.CanGoBack;
    public bool CanGoForward => _history.CanGoForward;
    public bool CanGoUp => CurrentPath.Length > 0 && Directory.GetParent(CurrentPath) is not null;

    public void RequestActivation() => ActivationRequested?.Invoke(this, EventArgs.Empty);

    /// <summary>Wird von der Ansicht aufgerufen, wenn sich die Auswahl in der Liste ändert.</summary>
    public void SetSelection(IEnumerable<FileEntryViewModel> selection)
    {
        _selection = selection.ToList();
        UpdateCommandStates();
        UpdateStatus();
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    public void SelectAll() => RequestSelection(Entries);

    public void SelectNone() => RequestSelection([]);

    public void InvertSelection() => RequestSelection(Entries.Except(_selection).ToList());

    private void RequestSelection(IReadOnlyList<FileEntryViewModel> entries)
    {
        if (SelectionRequested is null)
            SetSelection(entries); // ohne Ansicht (z. B. in Tests) direkt übernehmen
        else
            SelectionRequested.Invoke(this, entries);
    }

    public async Task NavigateAsync(string path, bool recordHistory = true, string? selectPath = null)
    {
        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(ExpandHome(path.Trim()),
                CurrentPath.Length > 0 ? CurrentPath : Environment.CurrentDirectory);
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
            var (entries, freeSpace) = await Task.Run(
                () => (_fileSystem.ListDirectory(fullPath, showHidden), GetFreeSpace(fullPath)), cts.Token);
            if (cts.IsCancellationRequested)
                return;

            if (recordHistory)
                _history.Navigate(fullPath);

            var pathChanged = !PathEquals(fullPath, CurrentPath);
            CurrentPath = fullPath;
            AddressText = fullPath;
            _allEntries = entries;
            _freeSpaceText = freeSpace is { } bytes ? $"{SizeFormatter.Format(bytes)} frei" : string.Empty;

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

    /// <summary>Liest den aktuellen Ordner neu ein und behält die Markierung bei.</summary>
    public Task ReloadAsync(string? selectPath = null) =>
        CurrentPath.Length == 0
            ? Task.CompletedTask
            : NavigateAsync(CurrentPath, recordHistory: false, selectPath: selectPath ?? SelectedEntry?.FullPath);

    [RelayCommand]
    private Task RefreshAsync() => ReloadAsync();

    [RelayCommand]
    private Task NavigateToAddressAsync() => NavigateAsync(AddressText);

    [RelayCommand]
    private void ResetAddress() => AddressText = CurrentPath;

    [RelayCommand(CanExecute = nameof(CanGoBack))]
    public Task GoBackAsync()
    {
        var previous = CurrentPath;
        return _history.GoBack() is { } path
            ? NavigateAsync(path, recordHistory: false, selectPath: previous)
            : Task.CompletedTask;
    }

    [RelayCommand(CanExecute = nameof(CanGoForward))]
    public Task GoForwardAsync() =>
        _history.GoForward() is { } path ? NavigateAsync(path, recordHistory: false) : Task.CompletedTask;

    [RelayCommand(CanExecute = nameof(CanGoUp))]
    public Task GoUpAsync() =>
        Directory.GetParent(CurrentPath) is { } parent
            ? NavigateAsync(parent.FullName, selectPath: CurrentPath)
            : Task.CompletedTask;

    public Task GoRootAsync() =>
        Path.GetPathRoot(CurrentPath) is { Length: > 0 } root ? NavigateAsync(root) : Task.CompletedTask;

    public Task NavigateToLocationAsync(LocationViewModel location) =>
        PathEquals(location.Path, CurrentPath) ? Task.CompletedTask : NavigateAsync(location.Path);

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
            _shell.Open(entry.FullPath);
        }
        catch (Exception ex)
        {
            await _dialogs.ShowErrorAsync("Datei kann nicht geöffnet werden", ex.Message);
        }
    }

    public async Task OpenSelectedAsync()
    {
        if (_selection is [var single])
        {
            await OpenEntryAsync(single);
            return;
        }

        foreach (var file in _selection.Where(e => !e.IsDirectory).ToList())
            await OpenEntryAsync(file);
    }

    partial void OnFilterTextChanged(string value) => ApplyView(SelectedEntry?.FullPath);

    partial void OnShowHiddenChanged(bool value) => _ = ReloadAsync();

    partial void OnCurrentPathChanged(string value)
    {
        // Ort anzeigen, in dem der Ordner liegt (längster passender Pfad)
        _syncingLocation = true;
        SelectedLocation = Locations
            .Where(l => IsSameOrInside(value, l.Path))
            .MaxBy(l => l.Path.Length);
        _syncingLocation = false;
    }

    partial void OnSelectedLocationChanged(LocationViewModel? value)
    {
        if (!_syncingLocation && value is not null)
            _ = NavigateToLocationAsync(value);
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

        if (_freeSpaceText.Length > 0)
            parts.Add(_freeSpaceText);

        StatusText = string.Join("   ·   ", parts);
    }

    private void UpdateCommandStates()
    {
        GoBackCommand.NotifyCanExecuteChanged();
        GoForwardCommand.NotifyCanExecuteChanged();
        GoUpCommand.NotifyCanExecuteChanged();
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
            // Ohne Überwachung bleibt nur das manuelle Aktualisieren
        }
    }

    // Kommt auf einem Hintergrund-Thread an; mehrere Ereignisse werden zu einer Aktualisierung gebündelt
    private void OnFileSystemChanged(object? sender, FileSystemEventArgs e) =>
        Dispatcher.UIThread.Post(() =>
        {
            _refreshTimer.Stop();
            _refreshTimer.Start();
        });

    private static long? GetFreeSpace(string path)
    {
        try
        {
            return new DriveInfo(path).AvailableFreeSpace;
        }
        catch (Exception ex) when (ex is IOException or ArgumentException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static string ExpandHome(string path) =>
        path == "~" || path.StartsWith("~/", StringComparison.Ordinal)
            ? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + path[1..]
            : path;

    private static StringComparison PathComparison =>
        OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

    internal static bool PathEquals(string? a, string? b) =>
        a is not null && b is not null &&
        string.Equals(Path.TrimEndingDirectorySeparator(a), Path.TrimEndingDirectorySeparator(b), PathComparison);

    private static bool IsSameOrInside(string path, string folder)
    {
        if (PathEquals(path, folder))
            return true;
        var prefix = Path.TrimEndingDirectorySeparator(folder) + Path.DirectorySeparatorChar;
        // Wurzel "/" bzw. "C:\" endet bereits mit Trennzeichen
        if (prefix.EndsWith($"{Path.DirectorySeparatorChar}{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            prefix = prefix[..^1];
        return path.StartsWith(prefix, PathComparison);
    }

    public void Dispose()
    {
        _refreshTimer.Stop();
        _watcher?.Dispose();
        _loadCts?.Cancel();
        GC.SuppressFinalize(this);
    }
}
