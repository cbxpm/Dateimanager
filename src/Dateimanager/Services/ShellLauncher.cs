using System.Diagnostics;

namespace Dateimanager.Services;

public interface IFileLauncher
{
    void Open(string path);
}

/// <summary>Öffnet Dateien mit dem Standardprogramm des Betriebssystems.</summary>
public sealed class ShellLauncher : IFileLauncher
{
    public void Open(string path)
    {
        if (OperatingSystem.IsWindows())
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        else
            Process.Start(OperatingSystem.IsMacOS() ? "open" : "xdg-open", [path]);
    }
}
