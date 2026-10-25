namespace Dateimanager.Core;

/// <summary>Kapselt alle Dateisystem-Operationen des Dateimanagers.</summary>
public sealed class FileSystemService
{
    private static readonly char[] InvalidNameChars =
        Path.GetInvalidFileNameChars().Union(['/', '\\']).ToArray();

    public IReadOnlyList<FileSystemEntry> ListDirectory(string path, bool includeHidden)
    {
        var options = new EnumerationOptions
        {
            IgnoreInaccessible = true,
            RecurseSubdirectories = false,
            AttributesToSkip = includeHidden ? 0 : FileAttributes.Hidden | FileAttributes.System,
        };

        var result = new List<FileSystemEntry>();
        foreach (var info in new DirectoryInfo(path).EnumerateFileSystemInfos("*", options))
        {
            if (TryCreateEntry(info) is { } entry)
                result.Add(entry);
        }

        result.Sort(CompareFoldersFirst);
        return result;
    }

    public static int CompareFoldersFirst(FileSystemEntry a, FileSystemEntry b)
    {
        if (a.IsDirectory != b.IsDirectory)
            return a.IsDirectory ? -1 : 1;
        return StringComparer.CurrentCultureIgnoreCase.Compare(a.Name, b.Name);
    }

    private static FileSystemEntry? TryCreateEntry(FileSystemInfo info)
    {
        try
        {
            var isDirectory = info is DirectoryInfo;
            return new FileSystemEntry(
                info.Name,
                info.FullName,
                isDirectory,
                isDirectory ? null : ((FileInfo)info).Length,
                info.LastWriteTime,
                info.Attributes.HasFlag(FileAttributes.Hidden),
                info.LinkTarget is not null);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // z. B. defekte symbolische Links – Eintrag überspringen
            return null;
        }
    }

    /// <summary>Prüft einen Datei-/Ordnernamen und liefert eine Fehlermeldung oder null.</summary>
    public static string? ValidateName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "Der Name darf nicht leer sein.";
        if (name is "." or "..")
            return "Dieser Name ist nicht erlaubt.";
        if (name.IndexOfAny(InvalidNameChars) >= 0)
            return "Der Name enthält ungültige Zeichen.";
        return null;
    }

    /// <summary>Liefert einen im Verzeichnis noch freien Namen, z. B. "Bericht (2).txt".</summary>
    public static string GetUniqueName(string directory, string name)
    {
        if (!Exists(Path.Combine(directory, name)))
            return name;

        var isDirectory = Directory.Exists(Path.Combine(directory, name));
        var baseName = isDirectory ? name : Path.GetFileNameWithoutExtension(name);
        var extension = isDirectory ? string.Empty : Path.GetExtension(name);

        for (var i = 2; ; i++)
        {
            var candidate = $"{baseName} ({i}){extension}";
            if (!Exists(Path.Combine(directory, candidate)))
                return candidate;
        }
    }

    public string CreateDirectory(string parent, string name)
    {
        EnsureValidName(name);
        var path = Path.Combine(parent, name);
        if (Exists(path))
            throw new IOException($"„{name}“ existiert bereits.");
        Directory.CreateDirectory(path);
        return path;
    }

    public string Rename(string path, string newName)
    {
        EnsureValidName(newName);
        var parent = Path.GetDirectoryName(path)
            ?? throw new IOException("Ein Laufwerk kann nicht umbenannt werden.");
        var target = Path.Combine(parent, newName);

        if (string.Equals(path, target, StringComparison.Ordinal))
            return path;

        // Bei reiner Änderung der Groß-/Kleinschreibung meldet Exists() auf Windows/macOS das Original selbst
        var caseOnlyChange = string.Equals(path, target, StringComparison.OrdinalIgnoreCase);
        var targetExists = caseOnlyChange
            ? Directory.EnumerateFileSystemEntries(parent).Any(p => Path.GetFileName(p) == newName)
            : Exists(target);
        if (targetExists)
            throw new IOException($"„{newName}“ existiert bereits.");

        if (caseOnlyChange)
        {
            // Auf Dateisystemen ohne Groß-/Kleinschreibung über einen Zwischennamen umbenennen
            var temp = Path.Combine(parent, $".{Guid.NewGuid():N}.tmp");
            MoveEntry(path, temp);
            MoveEntry(temp, target);
        }
        else
        {
            MoveEntry(path, target);
        }

        return target;
    }

    /// <summary>Kopiert Dateien/Ordner in das Zielverzeichnis. Namenskonflikte werden durch Umbenennen gelöst.</summary>
    public IReadOnlyList<string> Copy(IEnumerable<string> sources, string destinationDirectory)
    {
        var created = new List<string>();
        foreach (var source in sources)
        {
            EnsureNotIntoItself(source, destinationDirectory);
            var target = Path.Combine(destinationDirectory,
                GetUniqueName(destinationDirectory, Path.GetFileName(TrimEnd(source))));

            if (Directory.Exists(source))
                CopyDirectory(source, target);
            else
                File.Copy(source, target);

            created.Add(target);
        }

        return created;
    }

    /// <summary>Verschiebt Dateien/Ordner in das Zielverzeichnis.</summary>
    public IReadOnlyList<string> Move(IEnumerable<string> sources, string destinationDirectory)
    {
        var moved = new List<string>();
        foreach (var source in sources)
        {
            EnsureNotIntoItself(source, destinationDirectory);
            var name = Path.GetFileName(TrimEnd(source));

            if (PathsEqual(Path.GetDirectoryName(TrimEnd(source)), destinationDirectory))
            {
                moved.Add(source); // bereits am Ziel
                continue;
            }

            var target = Path.Combine(destinationDirectory, GetUniqueName(destinationDirectory, name));
            if (Directory.Exists(source) && !IsSymbolicLink(source))
            {
                try
                {
                    Directory.Move(source, target);
                }
                catch (IOException) when (!Exists(target))
                {
                    // Verschieben über Laufwerksgrenzen: kopieren und Quelle löschen
                    CopyDirectory(source, target);
                    Directory.Delete(source, recursive: true);
                }
            }
            else
            {
                File.Move(source, target);
            }

            moved.Add(target);
        }

        return moved;
    }

    /// <summary>Löscht Dateien/Ordner endgültig (ohne Papierkorb).</summary>
    public void DeletePermanently(IEnumerable<string> paths)
    {
        foreach (var path in paths)
        {
            if (Directory.Exists(path) && !IsSymbolicLink(path))
                Directory.Delete(path, recursive: true);
            else
                File.Delete(path);
        }
    }

    private static void CopyDirectory(string source, string target)
    {
        Directory.CreateDirectory(target);
        var options = new EnumerationOptions
        {
            AttributesToSkip = 0,
            RecurseSubdirectories = false,
            IgnoreInaccessible = false,
        };

        foreach (var info in new DirectoryInfo(source).EnumerateFileSystemInfos("*", options))
        {
            var destination = Path.Combine(target, info.Name);
            if (info is DirectoryInfo dir)
            {
                // Symbolische Links auf Ordner als Link kopieren, um Endlosschleifen zu vermeiden
                if (dir.LinkTarget is { } linkTarget)
                    Directory.CreateSymbolicLink(destination, linkTarget);
                else
                    CopyDirectory(dir.FullName, destination);
            }
            else
            {
                File.Copy(info.FullName, destination);
            }
        }
    }

    private static void MoveEntry(string source, string target)
    {
        if (Directory.Exists(source) && !IsSymbolicLink(source))
            Directory.Move(source, target);
        else
            File.Move(source, target);
    }

    private static void EnsureNotIntoItself(string source, string destinationDirectory)
    {
        if (!Directory.Exists(source) || IsSymbolicLink(source))
            return;

        var src = TrimEnd(Path.GetFullPath(source)) + Path.DirectorySeparatorChar;
        var dst = TrimEnd(Path.GetFullPath(destinationDirectory)) + Path.DirectorySeparatorChar;
        if (dst.StartsWith(src, PathComparison))
            throw new IOException($"„{Path.GetFileName(TrimEnd(source))}“ kann nicht in sich selbst kopiert oder verschoben werden.");
    }

    private static void EnsureValidName(string name)
    {
        if (ValidateName(name) is { } error)
            throw new ArgumentException(error, nameof(name));
    }

    private static bool Exists(string path) =>
        File.Exists(path) || Directory.Exists(path) || IsSymbolicLink(path);

    private static bool IsSymbolicLink(string path)
    {
        try
        {
            return new FileInfo(path).LinkTarget is not null;
        }
        catch (IOException)
        {
            return false;
        }
    }

    private static bool PathsEqual(string? a, string? b) =>
        a is not null && b is not null &&
        string.Equals(TrimEnd(Path.GetFullPath(a)), TrimEnd(Path.GetFullPath(b)), PathComparison);

    private static string TrimEnd(string path) =>
        path.Length > 1 && path != Path.GetPathRoot(path)
            ? path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            : path;

    private static StringComparison PathComparison =>
        OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
}
