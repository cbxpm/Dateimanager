using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Dateimanager.Core;
using Dateimanager.Services;
using Dateimanager.ViewModels;
using Dateimanager.Views;

namespace Dateimanager;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var window = new MainWindow();
            var startPath = desktop.Args is [var arg, ..] && Directory.Exists(arg)
                ? Path.GetFullPath(arg)
                : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

            var viewModel = new MainWindowViewModel(
                new FileSystemService(),
                TrashService.CreateForCurrentPlatform(),
                new DialogService(window),
                new ShellLauncher());

            window.DataContext = viewModel;
            desktop.MainWindow = window;
            _ = viewModel.NavigateAsync(startPath);
        }

        base.OnFrameworkInitializationCompleted();
    }
}
