using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Dateimanager.ViewModels;

namespace Dateimanager.Views;

public partial class MainWindow : Window
{
    private MainWindowViewModel? _viewModel;

    public MainWindow()
    {
        InitializeComponent();
        // Tunnel, damit Tab nicht zur Fokus-Navigation verwendet wird
        AddHandler(KeyDownEvent, OnPreviewKeyDown, RoutingStrategies.Tunnel);
        Opened += (_, _) => ActivePaneView?.FocusGrid();
    }

    private PaneView? ActivePaneView =>
        _viewModel is null ? null : _viewModel.ActivePane == _viewModel.Left ? LeftPane : RightPane;

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (_viewModel is not null)
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;

        _viewModel = DataContext as MainWindowViewModel;
        if (_viewModel is null)
            return;

        LeftPane.Attach(_viewModel, _viewModel.Left);
        RightPane.Attach(_viewModel, _viewModel.Right);
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        _viewModel.Left.PropertyChanged += OnPanePropertyChanged;
        _viewModel.Right.PropertyChanged += OnPanePropertyChanged;
        UpdateTitle();
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        _viewModel?.SaveSettings();
        base.OnClosing(e);
    }

    protected override void OnClosed(EventArgs e)
    {
        _viewModel?.Dispose();
        base.OnClosed(e);
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainWindowViewModel.ActivePane))
            UpdateTitle();
    }

    private void OnPanePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PaneViewModel.CurrentPath) && sender == _viewModel?.ActivePane)
            UpdateTitle();
    }

    private void UpdateTitle() =>
        Title = _viewModel?.ActivePane.CurrentPath is { Length: > 0 } path ? $"{path} – Dateimanager" : "Dateimanager";

    private void OnPreviewKeyDown(object? sender, KeyEventArgs e)
    {
        if (_viewModel is null)
            return;

        switch (e.Key, e.KeyModifiers)
        {
            case (Key.Tab, KeyModifiers.None):
                _viewModel.SwitchPaneCommand.Execute(null);
                ActivePaneView?.FocusGrid();
                e.Handled = true;
                break;
            case (Key.L, KeyModifiers.Control):
                ActivePaneView?.FocusAddress();
                e.Handled = true;
                break;
            case (Key.F, KeyModifiers.Control):
                ActivePaneView?.FocusFilter();
                e.Handled = true;
                break;
        }
    }

    private void OnExitClick(object? sender, RoutedEventArgs e) => Close();
}
