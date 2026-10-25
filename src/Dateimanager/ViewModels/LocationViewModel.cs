using Avalonia.Media;
using Dateimanager.Core;

namespace Dateimanager.ViewModels;

public sealed class LocationViewModel(Location location) : ViewModelBase
{
    public string Name => location.Name;
    public string Path => location.Path;

    public Geometry Icon => location.Kind switch
    {
        LocationKind.Home => Icons.Home,
        LocationKind.Drive => Icons.Drive,
        _ => Icons.Folder,
    };
}
