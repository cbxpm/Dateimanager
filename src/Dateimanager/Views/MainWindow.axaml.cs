using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Dateimanager.ViewModels;

namespace Dateimanager.Views;

public partial class MainWindow : Window
{
    private static readonly Dictionary<SortColumn, string> ColumnTitles = new()
    {
        [SortColumn.Name] = "Name",
        [SortColumn.Size] = "Größe",
        [SortColumn.Type] = "Typ",
        [SortColumn.Modified] = "Geändert am",
    };

    private MainWindowViewModel? _viewModel;

    public MainWindow()
    {
        InitializeComponent();

        FileGrid.SelectionChanged += (_, _) =>
            _viewModel?.SetSelection(FileGrid.SelectedItems.OfType<FileEntryViewModel>());
        FileGrid.DoubleTapped += OnGridDoubleTapped;
        FileGrid.Sorting += OnGridSorting;
        // Tunnel-Handler, weil das DataGrid Enter/Rücktaste sonst selbst verarbeitet
        FileGrid.AddHandler(KeyDownEvent, OnGridKeyDown, RoutingStrategies.Tunnel);
        FileGrid.AddHandler(PointerPressedEvent, OnGridPointerPressed, RoutingStrategies.Tunnel);
        KeyDown += OnWindowKeyDown;
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (_viewModel is not null)
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;

        _viewModel = DataContext as MainWindowViewModel;

        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
            UpdateColumnHeaders();
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        _viewModel?.Dispose();
        base.OnClosed(e);
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(MainWindowViewModel.SortColumn) or nameof(MainWindowViewModel.SortAscending):
                UpdateColumnHeaders();
                break;
            case nameof(MainWindowViewModel.SelectedEntry) when _viewModel?.SelectedEntry is { } entry:
                FileGrid.ScrollIntoView(entry, null);
                break;
            case nameof(MainWindowViewModel.CurrentPath):
                FileGrid.Focus();
                break;
        }
    }

    private void OnGridSorting(object? sender, DataGridColumnEventArgs e)
    {
        // Sortierung übernimmt das ViewModel, damit Ordner immer oben bleiben
        e.Handled = true;
        if (Enum.TryParse<SortColumn>(e.Column.SortMemberPath, out var column))
            _viewModel?.SortByCommand.Execute(column);
    }

    private void UpdateColumnHeaders()
    {
        if (_viewModel is null)
            return;

        foreach (var column in FileGrid.Columns)
        {
            if (!Enum.TryParse<SortColumn>(column.SortMemberPath, out var key))
                continue;
            var arrow = key == _viewModel.SortColumn ? (_viewModel.SortAscending ? "  ↑" : "  ↓") : string.Empty;
            column.Header = ColumnTitles[key] + arrow;
        }
    }

    private void OnGridDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (_viewModel is null)
            return;

        var row = (e.Source as Control)?.FindAncestorOfType<DataGridRow>(includeSelf: true);
        if (row?.DataContext is FileEntryViewModel entry)
            _viewModel.OpenEntryCommand.Execute(entry);
    }

    private void OnGridPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        // Rechtsklick auf eine nicht ausgewählte Zeile wählt diese aus (wie im Explorer)
        if (!e.GetCurrentPoint(FileGrid).Properties.IsRightButtonPressed)
            return;

        var row = (e.Source as Control)?.FindAncestorOfType<DataGridRow>(includeSelf: true);
        if (row?.DataContext is FileEntryViewModel entry && !FileGrid.SelectedItems.Contains(entry))
            FileGrid.SelectedItem = entry;
    }

    private void OnGridKeyDown(object? sender, KeyEventArgs e)
    {
        if (_viewModel is null || e.KeyModifiers != KeyModifiers.None)
            return;

        switch (e.Key)
        {
            case Key.Enter:
                _viewModel.OpenSelectedCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.Back:
                _viewModel.GoUpCommand.Execute(null);
                e.Handled = true;
                break;
        }
    }

    private void OnWindowKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyModifiers != KeyModifiers.Control)
            return;

        TextBox? target = e.Key switch
        {
            Key.L => AddressBox,
            Key.F => FilterBox,
            _ => null,
        };

        if (target is not null)
        {
            target.Focus();
            target.SelectAll();
            e.Handled = true;
        }
    }
}
