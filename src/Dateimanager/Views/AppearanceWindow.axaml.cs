using Avalonia.Controls;
using Avalonia.Media;
using Dateimanager.Core;
using Dateimanager.ViewModels;

namespace Dateimanager.Views;

public partial class AppearanceWindow : Window
{
    public AppearanceWindow()
    {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (DataContext is AppearanceDialogViewModel viewModel)
            viewModel.CloseRequested += (_, result) => Close(result);
    }

    public static async Task<AppearanceSettings?> ShowAsync(Window owner, AppearanceSettings settings)
    {
        var families = FontManager.Current.SystemFonts.Select(f => f.Name).ToList();
        var window = new AppearanceWindow { DataContext = new AppearanceDialogViewModel(settings, families) };
        return await window.ShowDialog<AppearanceSettings?>(owner);
    }
}
