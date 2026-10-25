using Avalonia.Headless.XUnit;
using Dateimanager.ViewModels;
using static Dateimanager.Tests.TestEnvironment;

namespace Dateimanager.Tests;

public sealed class PaneViewModelTests : IDisposable
{
    private readonly TestEnvironment _env = new();
    private TempDirectory Temp => _env.Temp;
    private PaneViewModel Pane => _env.Left;

    public void Dispose() => _env.Dispose();

    [AvaloniaFact]
    public async Task Navigate_ListsEntriesAndUpdatesState()
    {
        Temp.File("b.txt");
        Temp.Dir("a");

        await Pane.NavigateAsync(Temp.Path);

        Assert.Equal(["a", "b.txt"], Names(Pane));
        Assert.Equal(Temp.Path, Pane.CurrentPath);
        Assert.Equal(Temp.Path, Pane.AddressText);
        Assert.Equal("Test", Pane.SelectedLocation?.Name);
        Assert.StartsWith("2 Elemente", Pane.StatusText);
    }

    [AvaloniaFact]
    public async Task SelectedLocation_ShowsEnclosingLocationForSubfolders()
    {
        var sub = Temp.Dir("tief/drin");

        await Pane.NavigateAsync(sub);

        Assert.Equal("Test", Pane.SelectedLocation?.Name);
        Assert.Equal(sub, Pane.CurrentPath); // Synchronisieren der Ortsauswahl navigiert nicht
    }

    [AvaloniaFact]
    public async Task Navigate_ToMissingFolderShowsErrorAndKeepsLocation()
    {
        await Pane.NavigateAsync(Temp.Path);
        Pane.AddressText = Temp.Combine("gibt-es-nicht");

        await Pane.NavigateToAddressCommand.ExecuteAsync(null);

        Assert.Single(_env.Dialogs.Errors);
        Assert.Equal(Temp.Path, Pane.CurrentPath);
        Assert.Equal(Temp.Path, Pane.AddressText);
    }

    [AvaloniaFact]
    public async Task OpenUpBackAndForward()
    {
        var sub = Temp.Dir("sub");
        await Pane.NavigateAsync(Temp.Path);

        await Pane.OpenEntryAsync(Pane.Entries.Single(e => e.Name == "sub"));
        Assert.Equal(sub, Pane.CurrentPath);

        await Pane.GoUpCommand.ExecuteAsync(null);
        Assert.Equal(Temp.Path, Pane.CurrentPath);
        Assert.Equal("sub", Pane.SelectedEntry?.Name); // zuletzt besuchter Ordner wird markiert

        await Pane.GoBackCommand.ExecuteAsync(null);
        Assert.Equal(sub, Pane.CurrentPath);
        Assert.True(Pane.GoForwardCommand.CanExecute(null));

        await Pane.GoForwardCommand.ExecuteAsync(null);
        Assert.Equal(Temp.Path, Pane.CurrentPath);
    }

    [AvaloniaFact]
    public async Task GoRoot_NavigatesToFileSystemRoot()
    {
        await Pane.NavigateAsync(Temp.Path);

        await Pane.GoRootAsync();

        Assert.Equal(Path.GetPathRoot(Temp.Path), Pane.CurrentPath);
    }

    [AvaloniaFact]
    public async Task OpenFile_UsesShell()
    {
        var file = Temp.File("bild.png");
        await Pane.NavigateAsync(Temp.Path);
        Select(Pane, "bild.png");

        await Pane.OpenSelectedAsync();

        Assert.Equal([file], _env.Shell.Opened);
    }

    [AvaloniaFact]
    public async Task Filter_ReducesVisibleEntries()
    {
        Temp.File("Rechnung.pdf");
        Temp.File("Foto.jpg");
        await Pane.NavigateAsync(Temp.Path);

        Pane.FilterText = "rech";

        Assert.Equal(["Rechnung.pdf"], Names(Pane));
        Assert.StartsWith("1 von 2 Elementen", Pane.StatusText);
    }

    [AvaloniaFact]
    public async Task SortBySizeDescending_KeepsFoldersFirst()
    {
        Temp.File("klein.txt", "1");
        Temp.File("gross.txt", "1234567890");
        Temp.Dir("ordner");
        await Pane.NavigateAsync(Temp.Path);

        Pane.SortByCommand.Execute(SortColumn.Size);
        Assert.Equal(["ordner", "klein.txt", "gross.txt"], Names(Pane));

        Pane.SortByCommand.Execute(SortColumn.Size);
        Assert.False(Pane.SortAscending);
        Assert.Equal(["ordner", "gross.txt", "klein.txt"], Names(Pane));
    }

    [AvaloniaFact]
    public async Task SelectAllNoneAndInvert()
    {
        Temp.File("a");
        Temp.File("b");
        Temp.File("c");
        await Pane.NavigateAsync(Temp.Path);

        Pane.SelectAll();
        Assert.Equal(3, Pane.Selection.Count);

        Select(Pane, "a");
        Pane.InvertSelection();
        Assert.Equal(["b", "c"], Pane.Selection.Select(e => e.Name));

        Pane.SelectNone();
        Assert.Empty(Pane.Selection);
    }
}
