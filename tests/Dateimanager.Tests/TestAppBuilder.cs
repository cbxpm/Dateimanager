using Avalonia;
using Avalonia.Headless;

[assembly: AvaloniaTestApplication(typeof(Dateimanager.Tests.TestAppBuilder))]

namespace Dateimanager.Tests;

public static class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>().UseHeadless(new AvaloniaHeadlessPlatformOptions());
}
