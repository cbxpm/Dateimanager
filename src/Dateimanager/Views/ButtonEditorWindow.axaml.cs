using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Dateimanager.Core;
using Dateimanager.ViewModels;

namespace Dateimanager.Views;

public partial class ButtonEditorWindow : Window
{
    public ButtonEditorWindow()
    {
        InitializeComponent();
        Opened += (_, _) =>
        {
            LabelBox.Focus();
            LabelBox.SelectAll();
        };
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (DataContext is ButtonEditorViewModel viewModel)
            viewModel.CloseRequested += (_, result) => Close(result);
    }

    private async void OnBrowseProgram(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not ButtonEditorViewModel viewModel)
            return;

        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Programm auswählen",
            AllowMultiple = false,
        });

        if (files is [var file] && file.TryGetLocalPath() is { } path)
            viewModel.Program = path;
    }

    public static async Task<ButtonDefinition?> ShowAsync(Window owner, string title, ButtonDefinition button)
    {
        var window = new ButtonEditorWindow { Title = title, DataContext = new ButtonEditorViewModel(button) };
        return await window.ShowDialog<ButtonDefinition?>(owner);
    }
}
