using System.Globalization;
using Avalonia.Media;
using Dateimanager.Core;

namespace Dateimanager.ViewModels;

public sealed class FileEntryViewModel(FileSystemEntry entry) : ViewModelBase
{
    public FileSystemEntry Entry { get; } = entry;

    public string Name => Entry.Name;
    public string FullPath => Entry.FullPath;
    public bool IsDirectory => Entry.IsDirectory;
    public long Size => Entry.Size ?? -1;
    public DateTime LastModified => Entry.LastModified;

    public Geometry Icon => IsDirectory ? Icons.Folder : Icons.File;
    public double Opacity => Entry.IsHidden ? 0.55 : 1.0;

    public string SizeText => Entry.Size is { } size ? SizeFormatter.Format(size) : string.Empty;

    public string LastModifiedText => LastModified.ToString("g", CultureInfo.CurrentCulture);

    public string TypeText => IsDirectory
        ? "Dateiordner"
        : Entry.Extension.Length > 1
            ? $"{Entry.Extension[1..].ToUpperInvariant()}-Datei"
            : "Datei";
}
