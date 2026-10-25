using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Dateimanager.ViewModels;
using Dateimanager.Views;

namespace Dateimanager.Tests;

public sealed class MainWindowTests : IDisposable
{
    private readonly TestEnvironment _env = new();

    public void Dispose() => _env.Dispose();

    private async Task<MainWindow> ShowAsync(string left, string right)
    {
        var window = new MainWindow { DataContext = _env.ViewModel };
        window.Show();
        await _env.ViewModel.InitializeAsync(left, right);
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    private static DataGrid Grid(MainWindow window, string pane) =>
        window.FindControl<PaneView>(pane)!.Grid;

    [AvaloniaFact]
    public async Task Window_ShowsBothPanesAndButtonBank()
    {
        _env.Temp.File("links/datei.txt", "hallo");
        _env.Temp.Dir("links/ordner");
        var right = _env.Temp.Dir("rechts");

        var window = await ShowAsync(_env.Temp.Combine("links"), right);

        Assert.Equal(2, Grid(window, "LeftPane").ItemsSource!.Cast<object>().Count());
        Assert.Empty(Grid(window, "RightPane").ItemsSource!.Cast<object>());
        Assert.Equal("Name  ↑", Grid(window, "LeftPane").Columns[0].Header);

        var bank = window.FindControl<ItemsControl>("ButtonBankItems")!;
        Assert.Equal(16, bank.GetRealizedContainers().Count());
        window.Close();
    }

    [AvaloniaFact]
    public async Task GridSelection_IsReportedAndCanBeChangedByViewModel()
    {
        _env.Temp.File("a.txt");
        _env.Temp.File("b.txt");
        var window = await ShowAsync(_env.Temp.Path, _env.Temp.Path);
        var grid = Grid(window, "LeftPane");

        grid.SelectedItem = _env.Left.Entries[1];
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("b.txt", Assert.Single(_env.Left.Selection).Name);
        Assert.True(_env.ViewModel.RenameCommand.CanExecute(null));

        _env.Left.SelectAll();
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(2, grid.SelectedItems.Count);
        Assert.Equal(2, _env.Left.Selection.Count);
        window.Close();
    }

    [AvaloniaFact]
    public async Task TabSwitchesActivePane()
    {
        var window = await ShowAsync(_env.Temp.Path, _env.Temp.Path);
        Assert.Same(_env.Left, _env.ViewModel.ActivePane);

        window.KeyPressQwerty(PhysicalKey.Tab, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();

        Assert.Same(_env.Right, _env.ViewModel.ActivePane);
        Assert.Contains(_env.Right.CurrentPath, window.Title);
        window.Close();
    }
}
