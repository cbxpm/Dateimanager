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
    public void Expand_NamesWithoutPath() =>
        Assert.Equal(["-czf", "x.tgz", "a b.txt", "c.txt", "ls \"a b.txt\" \"c.txt\""],
            CommandTemplate.Expand("-czf x.tgz {o} \"ls {o}\"", Context));

    [Fact]
    public void UsesSelection()
    {
        Assert.True(CommandTemplate.UsesSelection("{s}"));
        Assert.True(CommandTemplate.UsesSelection("x{n}"));
        Assert.True(CommandTemplate.UsesSelection("{o}"));
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

        Assert.Equal(["Standard", "Extras"], settings.Banks.Select(b => b.Name));
        Assert.Equal(16, settings.Banks[0].Buttons.Count);
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
            ActiveBank = 1,
            Banks =
            [
                new ButtonBankDefinition { Name = "Eins", Columns = 6 },
                new ButtonBankDefinition
                {
                    Name = "Zwei",
                    Buttons = [ButtonDefinition.External("Gimp", "gimp", "{f}", "#8855AA", "#FFFFFF", runPerFile: true)],
                },
            ],
        };

        store.Save(settings);
        var loaded = store.Load();

        Assert.Equal("/a", loaded.LeftPath);
        Assert.True(loaded.ShowHidden);
        Assert.Equal(1, loaded.ActiveBank);
        Assert.Equal(["Eins", "Zwei"], loaded.Banks.Select(b => b.Name));
        Assert.Equal(6, loaded.Banks[0].Columns);
        Assert.Equal(settings.Banks[1].Buttons, loaded.Banks[1].Buttons);
        var json = File.ReadAllText(store.FilePath);
        Assert.Contains("\"kind\": \"External\"", json);
        Assert.DoesNotContain("\"buttonColumns\"", json);
    }

    [Fact]
    public void Load_BrokenFileFallsBackToDefaultsAndKeepsBackup()
    {
        var path = _temp.File("settings.json", "{ kaputt");

        var settings = new SettingsStore(path).Load();

        Assert.Equal(2, settings.Banks.Count);
        Assert.Equal("{ kaputt", File.ReadAllText(path + ".defekt"));
    }

    [Fact]
    public void Load_MigratesSingleButtonBarIntoFirstBank()
    {
        var path = _temp.File("settings.json", """
            {
              "leftPath": "/links",
              "buttonColumns": 5,
              "buttons": [ { "label": "Alt", "kind": "BuiltIn", "command": "Copy", "background": "#000000", "foreground": "#FFFFFF" } ]
            }
            """);

        var settings = new SettingsStore(path).Load();

        Assert.Equal("/links", settings.LeftPath);
        Assert.Equal(["Standard", "Extras"], settings.Banks.Select(b => b.Name));
        Assert.Equal(5, settings.Banks[0].Columns);
        Assert.Equal("Alt", Assert.Single(settings.Banks[0].Buttons).Label);
        Assert.Null(settings.LegacyButtons);
    }

    [Fact]
    public void Load_RepairsInvalidValues()
    {
        var path = _temp.File("settings.json", """{ "banks": [ { "name": "", "columns": 99 } ], "activeBank": 7 }""");

        var settings = new SettingsStore(path).Load();

        var bank = Assert.Single(settings.Banks);
        Assert.Equal("Bank", bank.Name);
        Assert.Equal(20, bank.Columns);
        Assert.Empty(bank.Buttons);
        Assert.Equal(0, settings.ActiveBank);
    }
}
