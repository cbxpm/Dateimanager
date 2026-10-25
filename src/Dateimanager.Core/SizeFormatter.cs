using System.Globalization;

namespace Dateimanager.Core;

public static class SizeFormatter
{
    private static readonly string[] Units = ["B", "KB", "MB", "GB", "TB", "PB"];

    public static string Format(long bytes, CultureInfo? culture = null)
    {
        culture ??= CultureInfo.CurrentCulture;
        if (bytes < 1024)
            return string.Format(culture, "{0} {1}", bytes, Units[0]);

        double value = bytes;
        var unit = 0;
        while (value >= 1024 && unit < Units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return string.Format(culture, value < 10 ? "{0:0.#} {1}" : "{0:0} {1}", value, Units[unit]);
    }
}
