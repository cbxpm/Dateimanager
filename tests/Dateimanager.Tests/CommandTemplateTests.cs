using Dateimanager.Core;

namespace Dateimanager.Tests;

public class CommandTemplateTests
{
    private static readonly CommandContext Context =
        new("/quelle", "/ziel mit leer", ["/quelle/a b.txt", "/quelle/c.txt"]);

    [Theory]
    [InlineData("", new string[0])]
    [InlineData("  -a   -b ", new[] { "-a", "-b" })]
    [InlineData("\"mit leer\" x", new[] { "mit leer", "x" })]
    [InlineData("\"\"", new[] { "" })]
    [InlineData("\"sag \"\"hallo\"\"\"", new[] { "sag \"hallo\"" })]
    [InlineData(@"C:\Programme\x.exe", new[] { @"C:\Programme\x.exe" })]
    public void Tokenize(string text, string[] expected) =>
        Assert.Equal(expected, CommandTemplate.Tokenize(text));

    [Fact]
    public void Expand_StandaloneFileListBecomesOneArgumentPerFile() =>
        Assert.Equal(["-o", "/ziel mit leer", "/quelle/a b.txt", "/quelle/c.txt"],
            CommandTemplate.Expand("-o {d} {f}", Context));

    [Fact]
    public void Expand_PlaceholdersInsideArguments() =>
        Assert.Equal(["--out=/ziel mit leer/a b.txt", "/quelle", "a b.txt"],
            CommandTemplate.Expand("\"--out={d}/{n}\" {p} {n}", Context));

    [Fact]
    public void Expand_EmbeddedFileListIsQuoted() =>
        Assert.Equal(["-c", "ls \"/quelle/a b.txt\" \"/quelle/c.txt\""],
            CommandTemplate.Expand("-c \"ls {f}\"", Context));

    [Fact]
    public void UsesSelection()
    {
        Assert.True(CommandTemplate.UsesSelection("{s}"));
        Assert.True(CommandTemplate.UsesSelection("x{n}"));
        Assert.False(CommandTemplate.UsesSelection("-a Terminal {p} {d}"));
    }
}

public sealed class SettingsStoreTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void Load_ReturnsDefaultsWhenFileIsMissing()
    {
        var settings = new SettingsStore(_temp.Combine("fehlt.json")).Load();

        Assert.Equal(8, settings.ButtonColumns);
        Assert.Equal(16, settings.Buttons.Count);
    }

    [Fact]
    public void SaveAndLoad_RoundTrips()
    {
        var store = new SettingsStore(_temp.Combine("sub/settings.json"));
        var settings = new AppSettings
        {
            LeftPath = "/a",
            RightPath = "/b",
            ShowHidden = true,
            ButtonColumns = 6,
            Buttons = [ButtonDefinition.External("Gimp", "gimp", "{f}", "#8855AA", "#FFFFFF", runPerFile: true)],
        };

        store.Save(settings);
        var loaded = store.Load();

        Assert.Equal("/a", loaded.LeftPath);
        Assert.True(loaded.ShowHidden);
        Assert.Equal(6, loaded.ButtonColumns);
        Assert.Equal(settings.Buttons, loaded.Buttons);
        Assert.Contains("\"kind\": \"External\"", File.ReadAllText(store.FilePath));
    }

    [Fact]
    public void Load_BrokenFileFallsBackToDefaultsAndKeepsBackup()
    {
        var path = _temp.File("settings.json", "{ kaputt");

        var settings = new SettingsStore(path).Load();

        Assert.Equal(16, settings.Buttons.Count);
        Assert.Equal("{ kaputt", File.ReadAllText(path + ".defekt"));
    }
}
