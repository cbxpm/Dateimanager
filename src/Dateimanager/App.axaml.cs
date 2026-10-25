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
            var store = new SettingsStore(SettingsStore.DefaultFilePath());
            var settings = store.Load();
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

            // Startordner: Kommandozeile, sonst zuletzt geöffnete Ordner, sonst persönlicher Ordner
            var args = desktop.Args ?? [];
            var leftPath = StartPath(args.ElementAtOrDefault(0), settings.LeftPath, home);
            var rightPath = StartPath(args.ElementAtOrDefault(1), settings.RightPath, home);

            var window = new MainWindow();
            var viewModel = new MainWindowViewModel(
                new FileSystemService(),
                TrashService.CreateForCurrentPlatform(),
                new DialogService(window),
                new ShellService(),
                store,
                settings);

            window.DataContext = viewModel;
            desktop.MainWindow = window;
            _ = viewModel.InitializeAsync(leftPath, rightPath);
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static string StartPath(string? argument, string? saved, string fallback)
    {
        if (!string.IsNullOrEmpty(argument) && Directory.Exists(argument))
            return Path.GetFullPath(argument);
        if (!string.IsNullOrEmpty(saved) && Directory.Exists(saved))
            return saved;
        return fallback;
    }
}
