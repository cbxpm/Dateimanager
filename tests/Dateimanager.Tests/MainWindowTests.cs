using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Dateimanager.Core;
using Location = Dateimanager.Core.Location;
using Dateimanager.ViewModels;
using Dateimanager.Views;

namespace Dateimanager.Tests;

public sealed class MainWindowTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    public void Dispose() => _temp.Dispose();

    [AvaloniaFact]
    public async Task Window_ShowsDirectoryContentsInGrid()
    {
        _temp.File("datei.txt", "hallo");
        _temp.Dir("ordner");

        var window = new MainWindow();
        var vm = new MainWindowViewModel(new FileSystemService(), new FakeTrash(), new FakeDialogService(),
            new FakeLauncher(), [new Location("Test", _temp.Path, LocationKind.Folder)]);
        window.DataContext = vm;
        window.Show();

        await vm.NavigateAsync(_temp.Path);
        Dispatcher.UIThread.RunJobs();

        var grid = window.FindControl<DataGrid>("FileGrid")!;
        Assert.Equal(2, grid.ItemsSource!.Cast<object>().Count());
        Assert.Equal("Name  ↑", grid.Columns[0].Header);

        // Auswahl in der Liste wird an das ViewModel gemeldet
        grid.SelectedItem = vm.Entries[1];
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("datei.txt", Assert.Single(vm.Selection).Name);
        Assert.True(vm.RenameCommand.CanExecute(null));

        window.Close();
    }
}
