using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
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
    public async Task CycleButton_SwitchesBankAndOffersBankList()
    {
        var window = await ShowAsync(_env.Temp.Path, _env.Temp.Path);
        var cycle = window.FindControl<Button>("BankCycleButton")!;
        var bank = window.FindControl<ItemsControl>("ButtonBankItems")!;

        cycle.Command!.Execute(null);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("Extras", _env.ViewModel.ButtonBar.CurrentBank.Name);
        Assert.Equal(8, bank.GetRealizedContainers().Count());

        cycle.ContextMenu!.Open(cycle);
        Dispatcher.UIThread.RunJobs();
        var items = cycle.ContextMenu.GetRealizedContainers().OfType<MenuItem>().ToList();
        Assert.Equal(["Standard", "Extras"], items.Select(i => i.Header));
        Assert.True(items[1].IsChecked);

        items[0].Command!.Execute(null);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("Standard", _env.ViewModel.ButtonBar.CurrentBank.Name);
        Assert.Equal(16, bank.GetRealizedContainers().Count());
        cycle.ContextMenu.Close();
        window.Close();
    }

    [AvaloniaFact]
    public async Task ButtonContextMenu_OffersMoveToOtherBank()
    {
        var window = await ShowAsync(_env.Temp.Path, _env.Temp.Path);
        var bank = window.FindControl<ItemsControl>("ButtonBankItems")!;
        var button = bank.GetRealizedContainers().Select(c => c.FindDescendantOfType<Button>()).First()!;

        button.ContextMenu!.Open(button);
        Dispatcher.UIThread.RunJobs();
        var moveMenu = button.ContextMenu.GetRealizedContainers().OfType<MenuItem>()
            .Single(i => (string?)i.Header == "In Knopfbank verschieben");
        moveMenu.Open();
        Dispatcher.UIThread.RunJobs();

        var target = moveMenu.GetRealizedContainers().OfType<MenuItem>().Single();
        Assert.Equal("Extras", target.Header);
        target.Command!.Execute(null);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("Verschieben", _env.ViewModel.ButtonBar.CurrentBank.Buttons[0].Label);
        button.ContextMenu.Close();
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
