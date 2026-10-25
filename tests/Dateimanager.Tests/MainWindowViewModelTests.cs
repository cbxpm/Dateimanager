using Avalonia.Headless.XUnit;
using Dateimanager.Core;
using Dateimanager.ViewModels;

namespace Dateimanager.Tests;

public sealed class MainWindowViewModelTests : IDisposable
{
    private readonly TempDirectory _temp = new();
    private readonly FakeDialogService _dialogs = new();
    private readonly FakeTrash _trash = new();
    private readonly FakeLauncher _launcher = new();
    private readonly MainWindowViewModel _vm;

    public MainWindowViewModelTests()
    {
        _vm = new MainWindowViewModel(new FileSystemService(), _trash, _dialogs, _launcher,
            [new Location("Test", _temp.Path, LocationKind.Folder)]);
    }

    public void Dispose()
    {
        _vm.Dispose();
        _temp.Dispose();
    }

    private string[] Names => _vm.Entries.Select(e => e.Name).ToArray();

    private void Select(params string[] names) =>
        _vm.SetSelection(_vm.Entries.Where(e => names.Contains(e.Name)));

    [AvaloniaFact]
    public async Task Navigate_ListsEntriesAndUpdatesState()
    {
        _temp.File("b.txt");
        _temp.Dir("a");

        await _vm.NavigateAsync(_temp.Path);

        Assert.Equal(["a", "b.txt"], Names);
        Assert.Equal(_temp.Path, _vm.CurrentPath);
        Assert.Equal(_temp.Path, _vm.AddressText);
        Assert.Equal("Test", _vm.SelectedLocation?.Name);
        Assert.Equal("2 Elemente", _vm.StatusText);
    }

    [AvaloniaFact]
    public async Task Navigate_ToMissingFolderShowsErrorAndKeepsLocation()
    {
        await _vm.NavigateAsync(_temp.Path);
        _vm.AddressText = _temp.Combine("gibt-es-nicht");

        await _vm.NavigateToAddressCommand.ExecuteAsync(null);

        Assert.Single(_dialogs.Errors);
        Assert.Equal(_temp.Path, _vm.CurrentPath);
        Assert.Equal(_temp.Path, _vm.AddressText);
    }

    [AvaloniaFact]
    public async Task OpenUpBackAndForward()
    {
        var sub = _temp.Dir("sub");
        await _vm.NavigateAsync(_temp.Path);

        await _vm.OpenEntryAsync(_vm.Entries.Single(e => e.Name == "sub"));
        Assert.Equal(sub, _vm.CurrentPath);
        Assert.Null(_vm.SelectedLocation);

        await _vm.GoUpCommand.ExecuteAsync(null);
        Assert.Equal(_temp.Path, _vm.CurrentPath);
        Assert.Equal("sub", _vm.SelectedEntry?.Name); // zuletzt besuchter Ordner wird markiert

        await _vm.GoBackCommand.ExecuteAsync(null);
        Assert.Equal(sub, _vm.CurrentPath);
        Assert.True(_vm.GoForwardCommand.CanExecute(null));

        await _vm.GoForwardCommand.ExecuteAsync(null);
        Assert.Equal(_temp.Path, _vm.CurrentPath);
    }

    [AvaloniaFact]
    public async Task OpenFile_UsesLauncher()
    {
        var file = _temp.File("bild.png");
        await _vm.NavigateAsync(_temp.Path);
        Select("bild.png");

        await _vm.OpenSelectedCommand.ExecuteAsync(null);

        Assert.Equal([file], _launcher.Opened);
    }

    [AvaloniaFact]
    public async Task Filter_ReducesVisibleEntries()
    {
        _temp.File("Rechnung.pdf");
        _temp.File("Foto.jpg");
        await _vm.NavigateAsync(_temp.Path);

        _vm.FilterText = "rech";

        Assert.Equal(["Rechnung.pdf"], Names);
        Assert.Equal("1 von 2 Elementen", _vm.StatusText);
    }

    [AvaloniaFact]
    public async Task SortBySizeDescending_KeepsFoldersFirst()
    {
        _temp.File("klein.txt", "1");
        _temp.File("gross.txt", "1234567890");
        _temp.Dir("ordner");
        await _vm.NavigateAsync(_temp.Path);

        _vm.SortByCommand.Execute(SortColumn.Size);
        Assert.Equal(["ordner", "klein.txt", "gross.txt"], Names);

        _vm.SortByCommand.Execute(SortColumn.Size);
        Assert.False(_vm.SortAscending);
        Assert.Equal(["ordner", "gross.txt", "klein.txt"], Names);
    }

    [AvaloniaFact]
    public async Task ShowHidden_RevealsDotFiles()
    {
        _temp.File(".versteckt");
        if (OperatingSystem.IsWindows())
            File.SetAttributes(_temp.Combine(".versteckt"), FileAttributes.Hidden);
        await _vm.NavigateAsync(_temp.Path);
        Assert.Empty(_vm.Entries);

        _vm.ToggleHiddenCommand.Execute(null);
        await _vm.RefreshAsync();

        Assert.Equal([".versteckt"], Names);
    }

    [AvaloniaFact]
    public async Task NewFolder_ProposesUniqueNameAndSelectsResult()
    {
        _temp.Dir("Neuer Ordner");
        await _vm.NavigateAsync(_temp.Path);
        _dialogs.PromptAnswers.Enqueue("Projekte");

        await _vm.NewFolderCommand.ExecuteAsync(null);

        Assert.Equal("Neuer Ordner (2)", _dialogs.Prompts.Single());
        Assert.True(Directory.Exists(_temp.Combine("Projekte")));
        Assert.Equal("Projekte", _vm.SelectedEntry?.Name);
    }

    [AvaloniaFact]
    public async Task Rename_RequiresSingleSelection()
    {
        _temp.File("a.txt");
        _temp.File("b.txt");
        await _vm.NavigateAsync(_temp.Path);

        Select("a.txt", "b.txt");
        Assert.False(_vm.RenameCommand.CanExecute(null));

        Select("a.txt");
        _dialogs.PromptAnswers.Enqueue("c.txt");
        await _vm.RenameCommand.ExecuteAsync(null);

        Assert.Equal(["b.txt", "c.txt"], Names);
        Assert.Equal("c.txt", _vm.SelectedEntry?.Name);
    }

    [AvaloniaFact]
    public async Task CopyAndPaste_DuplicatesIntoCurrentFolder()
    {
        _temp.File("a.txt", "x");
        await _vm.NavigateAsync(_temp.Path);
        Assert.False(_vm.PasteCommand.CanExecute(null));

        Select("a.txt");
        _vm.CopyCommand.Execute(null);
        await _vm.PasteCommand.ExecuteAsync(null);
        await _vm.PasteCommand.ExecuteAsync(null);

        Assert.Equal(["a (2).txt", "a (3).txt", "a.txt"], Names);
        Assert.True(_vm.PasteCommand.CanExecute(null));
    }

    [AvaloniaFact]
    public async Task CutAndPaste_MovesAndClearsClipboard()
    {
        _temp.File("a.txt", "x");
        var target = _temp.Dir("ziel");
        await _vm.NavigateAsync(_temp.Path);

        Select("a.txt");
        _vm.CutCommand.Execute(null);
        Assert.Contains("Ausschneiden", _vm.StatusText);

        await _vm.NavigateAsync(target);
        await _vm.PasteCommand.ExecuteAsync(null);

        Assert.Equal(["a.txt"], Names);
        Assert.False(File.Exists(_temp.Combine("a.txt")));
        Assert.False(_vm.PasteCommand.CanExecute(null));
    }

    [AvaloniaFact]
    public async Task MoveToTrash_OnlyAfterConfirmation()
    {
        var file = _temp.File("weg.txt");
        await _vm.NavigateAsync(_temp.Path);
        Select("weg.txt");

        _dialogs.ConfirmAnswer = false;
        await _vm.MoveToTrashCommand.ExecuteAsync(null);
        Assert.Empty(_trash.Trashed);

        _dialogs.ConfirmAnswer = true;
        await _vm.MoveToTrashCommand.ExecuteAsync(null);
        Assert.Equal([file], _trash.Trashed);
        Assert.Empty(_vm.Entries);
    }

    [AvaloniaFact]
    public async Task FailedOperation_ShowsError()
    {
        _temp.File("a.txt");
        await _vm.NavigateAsync(_temp.Path);
        Select("a.txt");
        File.Delete(_temp.Combine("a.txt")); // extern gelöscht

        _dialogs.PromptAnswers.Enqueue("b.txt");
        await _vm.RenameCommand.ExecuteAsync(null);

        Assert.Single(_dialogs.Errors);
        Assert.Empty(_vm.Entries);
    }
}
