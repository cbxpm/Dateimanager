namespace Dateimanager.Core;

/// <summary>Ein Eintrag (Datei oder Ordner) in einem Verzeichnis.</summary>
public sealed record FileSystemEntry(
    string Name,
    string FullPath,
    bool IsDirectory,
    long? Size,
    DateTime LastModified,
    bool IsHidden,
    bool IsSymbolicLink)
{
    public string Extension => IsDirectory ? string.Empty : Path.GetExtension(Name);
}
