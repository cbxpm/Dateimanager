using System.Diagnostics;

namespace Dateimanager.Services;

public interface IShellService
{
    /// <summary>Öffnet eine Datei mit dem Standardprogramm des Betriebssystems.</summary>
    void Open(string path);

    /// <summary>Startet ein Programm mit den angegebenen Argumenten.</summary>
    void Run(string program, IReadOnlyList<string> arguments, string workingDirectory);
}

public sealed class ShellService : IShellService
{
    public void Open(string path)
    {
        if (OperatingSystem.IsWindows())
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        else
            Process.Start(OperatingSystem.IsMacOS() ? "open" : "xdg-open", [path]);
    }

    public void Run(string program, IReadOnlyList<string> arguments, string workingDirectory)
    {
        var startInfo = new ProcessStartInfo(program)
        {
            UseShellExecute = false,
            WorkingDirectory = workingDirectory,
        };
        foreach (var argument in arguments)
            startInfo.ArgumentList.Add(argument);

        using var _ = Process.Start(startInfo);
    }
}
