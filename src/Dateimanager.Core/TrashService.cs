using System.Globalization;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;

namespace Dateimanager.Core;

/// <summary>Verschiebt Dateien und Ordner in den Papierkorb des Betriebssystems.</summary>
public interface ITrashService
{
    void MoveToTrash(string path);
}

public static class TrashService
{
    public static ITrashService CreateForCurrentPlatform()
    {
        if (OperatingSystem.IsWindows())
            return new WindowsRecycleBin();
        if (OperatingSystem.IsMacOS())
            return new MacTrash(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".Trash"));
        return new FreedesktopTrash(FreedesktopTrash.DefaultTrashDirectory());
    }
}

/// <summary>Papierkorb nach der freedesktop.org-Spezifikation (Linux, BSD).</summary>
public sealed class FreedesktopTrash(string trashDirectory) : ITrashService
{
    public string TrashDirectory { get; } = trashDirectory;

    public static string DefaultTrashDirectory()
    {
        var dataHome = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
        if (string.IsNullOrEmpty(dataHome))
            dataHome = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share");
        return Path.Combine(dataHome, "Trash");
    }

    public void MoveToTrash(string path)
    {
        var fullPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        var filesDir = Path.Combine(TrashDirectory, "files");
        var infoDir = Path.Combine(TrashDirectory, "info");
        Directory.CreateDirectory(filesDir);
        Directory.CreateDirectory(infoDir);

        var name = Path.GetFileName(fullPath);
        string trashName;
        FileStream infoStream;
        for (var i = 1; ; i++)
        {
            trashName = i == 1 ? name : $"{Path.GetFileNameWithoutExtension(name)}.{i}{Path.GetExtension(name)}";
            try
            {
                // Die .trashinfo-Datei wird exklusiv angelegt und reserviert so den Namen
                infoStream = new FileStream(Path.Combine(infoDir, trashName + ".trashinfo"),
                    FileMode.CreateNew, FileAccess.Write);
            }
            catch (IOException) when (File.Exists(Path.Combine(infoDir, trashName + ".trashinfo")))
            {
                continue;
            }

            if (File.Exists(Path.Combine(filesDir, trashName)) || Directory.Exists(Path.Combine(filesDir, trashName)))
            {
                infoStream.Dispose();
                File.Delete(Path.Combine(infoDir, trashName + ".trashinfo"));
                continue;
            }

            break;
        }

        using (var writer = new StreamWriter(infoStream, new UTF8Encoding(false)))
        {
            writer.Write(
                $"[Trash Info]\nPath={EncodePath(fullPath)}\nDeletionDate={DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture)}\n");
        }

        try
        {
            var target = Path.Combine(filesDir, trashName);
            if (Directory.Exists(fullPath) && new DirectoryInfo(fullPath).LinkTarget is null)
                Directory.Move(fullPath, target);
            else
                File.Move(fullPath, target);
        }
        catch
        {
            File.Delete(Path.Combine(infoDir, trashName + ".trashinfo"));
            throw;
        }
    }

    internal static string EncodePath(string path)
    {
        var builder = new StringBuilder();
        foreach (var b in Encoding.UTF8.GetBytes(path))
        {
            var c = (char)b;
            if (char.IsAsciiLetterOrDigit(c) || c is '/' or '-' or '_' or '.' or '~')
                builder.Append(c);
            else
                builder.Append('%').Append(b.ToString("X2", CultureInfo.InvariantCulture));
        }

        return builder.ToString();
    }
}

/// <summary>Einfacher macOS-Papierkorb (~/.Trash).</summary>
public sealed class MacTrash(string trashDirectory) : ITrashService
{
    public void MoveToTrash(string path)
    {
        Directory.CreateDirectory(trashDirectory);
        var fullPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        var target = Path.Combine(trashDirectory,
            FileSystemService.GetUniqueName(trashDirectory, Path.GetFileName(fullPath)));

        if (Directory.Exists(fullPath) && new DirectoryInfo(fullPath).LinkTarget is null)
            Directory.Move(fullPath, target);
        else
            File.Move(fullPath, target);
    }
}

/// <summary>Windows-Papierkorb über SHFileOperation.</summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsRecycleBin : ITrashService
{
    private const uint FO_DELETE = 0x0003;
    private const ushort FOF_SILENT = 0x0004;
    private const ushort FOF_NOCONFIRMATION = 0x0010;
    private const ushort FOF_ALLOWUNDO = 0x0040;
    private const ushort FOF_NOERRORUI = 0x0400;

    public void MoveToTrash(string path)
    {
        var operation = new SHFILEOPSTRUCT
        {
            wFunc = FO_DELETE,
            // Liste muss doppelt nullterminiert sein
            pFrom = Path.GetFullPath(path) + "\0\0",
            fFlags = FOF_ALLOWUNDO | FOF_NOCONFIRMATION | FOF_SILENT | FOF_NOERRORUI,
        };

        var result = SHFileOperation(ref operation);
        if (result != 0 || operation.fAnyOperationsAborted)
            throw new IOException($"„{Path.GetFileName(path)}“ konnte nicht in den Papierkorb verschoben werden (Fehler {result}).");
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SHFILEOPSTRUCT
    {
        public IntPtr hwnd;
        public uint wFunc;
        public string pFrom;
        public string? pTo;
        public ushort fFlags;
        [MarshalAs(UnmanagedType.Bool)] public bool fAnyOperationsAborted;
        public IntPtr hNameMappings;
        public string? lpszProgressTitle;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHFileOperation(ref SHFILEOPSTRUCT lpFileOp);
}
