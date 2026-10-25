using System.Globalization;
using Dateimanager.Core;

namespace Dateimanager.Tests;

public class NavigationHistoryTests
{
    [Fact]
    public void BackAndForwardBehaveLikeABrowser()
    {
        var history = new NavigationHistory();
        history.Navigate("/a");
        history.Navigate("/b");
        history.Navigate("/c");

        Assert.Equal("/b", history.GoBack());
        Assert.Equal("/a", history.GoBack());
        Assert.False(history.CanGoBack);
        Assert.Equal("/b", history.GoForward());

        history.Navigate("/d");
        Assert.False(history.CanGoForward);
        Assert.Equal("/b", history.GoBack());
    }

    [Fact]
    public void NavigatingToCurrentPathDoesNotGrowHistory()
    {
        var history = new NavigationHistory();
        history.Navigate("/a");
        history.Navigate("/a");

        Assert.False(history.CanGoBack);
    }
}

public class SizeFormatterTests
{
    [Theory]
    [InlineData(0, "0 B")]
    [InlineData(1023, "1023 B")]
    [InlineData(1024, "1 KB")]
    [InlineData(1536, "1,5 KB")]
    [InlineData(10 * 1024 * 1024, "10 MB")]
    [InlineData(5L * 1024 * 1024 * 1024 * 1024, "5 TB")]
    public void FormatsWithGermanCulture(long bytes, string expected) =>
        Assert.Equal(expected, SizeFormatter.Format(bytes, CultureInfo.GetCultureInfo("de-DE")));
}
