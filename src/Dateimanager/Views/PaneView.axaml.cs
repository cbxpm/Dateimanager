using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Dateimanager.ViewModels;

namespace Dateimanager.Views;

public partial class PaneView : UserControl
{
    private static readonly Dictionary<SortColumn, string> ColumnTitles = new()
    {
        [SortColumn.Name] = "Name",
        [SortColumn.Size] = "Größe",
        [SortColumn.Type] = "Typ",
        [SortColumn.Modified] = "Geändert am",
    };

    private MainWindowViewModel? _host;
    private PaneViewModel? _pane;
    private bool _applyingSelection;

    public PaneView()
    {
        InitializeComponent();

        FileGrid.SelectionChanged += (_, _) =>
        {
            if (!_applyingSelection)
                _pane?.SetSelection(FileGrid.SelectedItems.OfType<FileEntryViewModel>());
        };
        FileGrid.DoubleTapped += OnGridDoubleTapped;
        FileGrid.Sorting += OnGridSorting;
        // Tunnel-Handler, weil das DataGrid Enter/Rücktaste/Esc sonst selbst verarbeitet
        FileGrid.AddHandler(KeyDownEvent, OnGridKeyDown, RoutingStrategies.Tunnel);
        FileGrid.AddHandler(PointerPressedEvent, OnGridPointerPressed, RoutingStrategies.Tunnel);

        ParentStripLeft.PointerPressed += OnParentStripPressed;
        ParentStripRight.PointerPressed += OnParentStripPressed;

        // Jeder Klick oder Fokus im Fenster macht es zum aktiven Fenster
        AddHandler(PointerPressedEvent, (_, _) => _pane?.RequestActivation(), RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(GotFocusEvent, (_, _) => _pane?.RequestActivation(), RoutingStrategies.Bubble, handledEventsToo: true);
    }

    public DataGrid Grid => FileGrid;

    /// <summary>Verbindet die Ansicht mit ihrem Fenster-ViewModel und dem Hauptfenster.</summary>
    public void Attach(MainWindowViewModel host, PaneViewModel pane)
    {
        if (_pane is not null)
        {
            _pane.PropertyChanged -= OnPanePropertyChanged;
            _pane.SelectionRequested -= OnSelectionRequested;
        }

        _host = host;
        _pane = pane;
        DataContext = pane;
        GridMenu.DataContext = host;
        pane.PropertyChanged += OnPanePropertyChanged;
        pane.SelectionRequested += OnSelectionRequested;
        UpdateColumnHeaders();
    }

    public void FocusGrid()
    {
        FileGrid.Focus();
        if (FileGrid.SelectedItem is null && _pane?.Entries is [var first, ..])
            FileGrid.SelectedItem = first;
    }

    public void FocusAddress()
    {
        AddressBox.Focus();
        AddressBox.SelectAll();
    }

    public void FocusFilter()
    {
        FilterBox.Focus();
        FilterBox.SelectAll();
    }

    private void OnPanePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(PaneViewModel.SortColumn) or nameof(PaneViewModel.SortAscending):
                UpdateColumnHeaders();
                break;
            case nameof(PaneViewModel.SelectedEntry) when _pane?.SelectedEntry is { } entry:
                FileGrid.ScrollIntoView(entry, null);
                break;
            case nameof(PaneViewModel.CurrentPath) when _pane?.IsActive == true:
                FileGrid.Focus();
                break;
        }
    }

    private void OnSelectionRequested(object? sender, IReadOnlyList<FileEntryViewModel> entries)
    {
        _applyingSelection = true;
        try
        {
            FileGrid.SelectedItems.Clear();
            foreach (var entry in entries)
                FileGrid.SelectedItems.Add(entry);
        }
        finally
        {
            _applyingSelection = false;
        }

        _pane?.SetSelection(FileGrid.SelectedItems.OfType<FileEntryViewModel>());
    }

    private void OnParentStripPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_pane is null)
            return;

        var properties = e.GetCurrentPoint(this).Properties;
        if (properties.IsLeftButtonPressed)
            _ = _pane.GoUpAsync();
        else if (properties.IsRightButtonPressed)
            _ = _pane.GoRootAsync();
        else
            return;

        FileGrid.Focus();
        e.Handled = true;
    }

    private void OnGridSorting(object? sender, DataGridColumnEventArgs e)
    {
        // Sortierung übernimmt das ViewModel, damit Ordner immer oben bleiben
        e.Handled = true;
        if (Enum.TryParse<SortColumn>(e.Column.SortMemberPath, out var column))
            _pane?.SortByCommand.Execute(column);
    }

    private void UpdateColumnHeaders()
    {
        if (_pane is null)
            return;

        foreach (var column in FileGrid.Columns)
        {
            if (!Enum.TryParse<SortColumn>(column.SortMemberPath, out var key))
                continue;
            var arrow = key == _pane.SortColumn ? (_pane.SortAscending ? "  ↑" : "  ↓") : string.Empty;
            column.Header = ColumnTitles[key] + arrow;
        }
    }

    private void OnGridDoubleTapped(object? sender, TappedEventArgs e)
    {
        var row = (e.Source as Control)?.FindAncestorOfType<DataGridRow>(includeSelf: true);
        if (row?.DataContext is FileEntryViewModel entry)
            _pane?.OpenEntryCommand.Execute(entry);
    }

    private void OnGridPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        // Rechtsklick auf eine nicht ausgewählte Zeile wählt diese aus
        if (!e.GetCurrentPoint(FileGrid).Properties.IsRightButtonPressed)
            return;

        var row = (e.Source as Control)?.FindAncestorOfType<DataGridRow>(includeSelf: true);
        if (row?.DataContext is FileEntryViewModel entry && !FileGrid.SelectedItems.Contains(entry))
            FileGrid.SelectedItem = entry;
    }

    private void OnGridKeyDown(object? sender, KeyEventArgs e)
    {
        if (_host is null || _pane is null)
            return;

        System.Windows.Input.ICommand? command = (e.Key, e.KeyModifiers) switch
        {
            (Key.Enter, KeyModifiers.None) => _host.OpenSelectedCommand,
            (Key.Back, KeyModifiers.None) => _pane.GoUpCommand,
            (Key.Escape, KeyModifiers.None) => _pane.GoUpCommand,
            (Key.Delete, KeyModifiers.None) => _host.MoveToTrashCommand,
            (Key.Delete, KeyModifiers.Shift) => _host.DeletePermanentlyCommand,
            (Key.C, KeyModifiers.Control) => _host.ClipboardCopyCommand,
            (Key.X, KeyModifiers.Control) => _host.ClipboardCutCommand,
            (Key.V, KeyModifiers.Control) => _host.PasteCommand,
            (Key.A, KeyModifiers.Control) => _host.SelectAllCommand,
            _ => null,
        };

        if (command is null)
            return;

        if (command.CanExecute(null))
            command.Execute(null);
        e.Handled = true;
    }
}
