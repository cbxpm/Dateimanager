using Avalonia;

namespace Dateimanager;

internal sealed class Program
{
    // Vor AppMain keine Avalonia-APIs verwenden: das Framework ist noch nicht initialisiert.
    [STAThread]
    public static void Main(string[] args) => BuildAvaloniaApp()
        .StartWithClassicDesktopLifetime(args);

    // Wird auch vom Designer/Previewer verwendet.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
#if DEBUG
            .WithDeveloperTools()
#endif
            .WithInterFont()
            .LogToTrace();
}
