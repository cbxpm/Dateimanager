namespace Dateimanager.Core;

public enum LocationKind
{
    Home,
    Folder,
    Drive,
}

/// <summary>Ein Schnellzugriffsziel in der Seitenleiste.</summary>
public sealed record Location(string Name, string Path, LocationKind Kind);

public static class KnownLocations
{
    private static readonly string[] IgnoredUnixMountPrefixes =
        ["/proc", "/sys", "/dev", "/run/user", "/run/lock", "/snap", "/boot/efi", "/var/lib"];

    public static IReadOnlyList<Location> Get()
    {
        var result = new List<Location>();
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        void AddFolder(string name, string? path, LocationKind kind = LocationKind.Folder)
        {
            if (!string.IsNullOrEmpty(path) && Directory.Exists(path) &&
                result.All(l => l.Path != path))
                result.Add(new Location(name, path, kind));
        }

        AddFolder("Persönlicher Ordner", home, LocationKind.Home);
        AddFolder("Desktop", Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory));
        AddFolder("Dokumente", Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));
        AddFolder("Downloads", Path.Combine(home, "Downloads"));
        AddFolder("Bilder", Environment.GetFolderPath(Environment.SpecialFolder.MyPictures));
        AddFolder("Musik", Environment.GetFolderPath(Environment.SpecialFolder.MyMusic));
        AddFolder("Videos", Environment.GetFolderPath(Environment.SpecialFolder.MyVideos));

        foreach (var drive in GetDrives())
            result.Add(drive);

        return result;
    }

    private static IEnumerable<Location> GetDrives()
    {
        DriveInfo[] drives;
        try
        {
            drives = DriveInfo.GetDrives();
        }
        catch (IOException)
        {
            yield break;
        }

        foreach (var drive in drives)
        {
            Location? location = null;
            try
            {
                if (!drive.IsReady || drive.DriveType is DriveType.Ram or DriveType.Unknown)
                    continue;

                var root = drive.RootDirectory.FullName;
                if (!OperatingSystem.IsWindows() && root != "/" &&
                    IgnoredUnixMountPrefixes.Any(p => root.StartsWith(p, StringComparison.Ordinal)))
                    continue;

                var label = string.IsNullOrWhiteSpace(drive.VolumeLabel) || drive.VolumeLabel == root
                    ? root
                    : $"{drive.VolumeLabel} ({root.TrimEnd('\\')})";
                location = new Location(root == "/" ? "Dateisystem" : label, root, LocationKind.Drive);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                continue;
            }

            yield return location;
        }
    }
}
