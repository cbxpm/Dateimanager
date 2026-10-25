using Dateimanager.Core;

namespace Dateimanager.Tests;

public sealed class FileSystemServiceTests : IDisposable
{
    private readonly TempDirectory _temp = new();
    private readonly FileSystemService _fs = new();

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void ListDirectory_PutsFoldersFirstAndSortsByName()
    {
        _temp.File("b.txt", "12345");
        _temp.File("A.txt");
        _temp.Dir("zeta");
        _temp.Dir("Alpha");

        var entries = _fs.ListDirectory(_temp.Path, includeHidden: false);

        Assert.Equal(["Alpha", "zeta", "A.txt", "b.txt"], entries.Select(e => e.Name));
        Assert.Null(entries[0].Size);
        Assert.Equal(5, entries[3].Size);
    }

    [Fact]
    public void ListDirectory_HidesHiddenEntriesUnlessRequested()
    {
        _temp.File("visible.txt");
        var hidden = _temp.File(".hidden");
        if (OperatingSystem.IsWindows())
            File.SetAttributes(hidden, FileAttributes.Hidden);

        Assert.DoesNotContain(_fs.ListDirectory(_temp.Path, false), e => e.Name == ".hidden");
        Assert.Contains(_fs.ListDirectory(_temp.Path, true), e => e.Name == ".hidden" && e.IsHidden);
    }

    [Theory]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData(".", false)]
    [InlineData("..", false)]
    [InlineData("a/b", false)]
    [InlineData("a\\b", false)]
    [InlineData("Bericht 2024.pdf", true)]
    [InlineData("Größe & Ähnliches", true)]
    public void ValidateName(string name, bool valid) =>
        Assert.Equal(valid, FileSystemService.ValidateName(name) is null);

    [Fact]
    public void GetUniqueName_AppendsCounterBeforeExtension()
    {
        _temp.File("a.txt");
        _temp.File("a (2).txt");
        _temp.Dir("Neuer Ordner");

        Assert.Equal("frei.txt", FileSystemService.GetUniqueName(_temp.Path, "frei.txt"));
        Assert.Equal("a (3).txt", FileSystemService.GetUniqueName(_temp.Path, "a.txt"));
        Assert.Equal("Neuer Ordner (2)", FileSystemService.GetUniqueName(_temp.Path, "Neuer Ordner"));
    }

    [Fact]
    public void CreateDirectory_FailsIfNameExists()
    {
        var path = _fs.CreateDirectory(_temp.Path, "Neu");

        Assert.True(Directory.Exists(path));
        Assert.Throws<IOException>(() => _fs.CreateDirectory(_temp.Path, "Neu"));
        Assert.Throws<ArgumentException>(() => _fs.CreateDirectory(_temp.Path, "a/b"));
    }

    [Fact]
    public void Rename_RenamesFilesAndFolders()
    {
        var file = _temp.File("alt.txt", "x");
        var dir = _temp.Dir("ordner");

        var renamedFile = _fs.Rename(file, "neu.txt");
        var renamedDir = _fs.Rename(dir, "Ordner");

        Assert.Equal("x", File.ReadAllText(renamedFile));
        Assert.False(File.Exists(file));
        Assert.Equal("Ordner", Path.GetFileName(renamedDir));
        Assert.Equal(["Ordner"], Directory.GetDirectories(_temp.Path).Select(Path.GetFileName));
    }

    [Fact]
    public void Rename_FailsIfTargetExists()
    {
        var file = _temp.File("a.txt");
        _temp.File("b.txt");

        Assert.Throws<IOException>(() => _fs.Rename(file, "b.txt"));
        Assert.True(File.Exists(file));
    }

    [Fact]
    public void Rename_CaseOnlyChangeFailsIfDistinctEntryExists()
    {
        if (OperatingSystem.IsWindows() || OperatingSystem.IsMacOS())
            return; // nur auf Dateisystemen mit Groß-/Kleinschreibung möglich

        var lower = _temp.File("datei.txt", "klein");
        _temp.File("Datei.txt", "groß");

        Assert.Throws<IOException>(() => _fs.Rename(lower, "Datei.txt"));
        Assert.Equal("klein", File.ReadAllText(lower));
    }

    [Fact]
    public void Copy_IntoSameFolderCreatesNumberedDuplicate()
    {
        var file = _temp.File("doc.txt", "inhalt");

        var created = _fs.Copy([file], _temp.Path);

        Assert.Equal(_temp.Combine("doc (2).txt"), Assert.Single(created));
        Assert.Equal("inhalt", File.ReadAllText(created[0]));
    }

    [Fact]
    public void Copy_CopiesFoldersRecursively()
    {
        _temp.File("quelle/a.txt", "a");
        _temp.File("quelle/sub/b.txt", "b");
        var target = _temp.Dir("ziel");

        _fs.Copy([_temp.Combine("quelle")], target);

        Assert.Equal("a", File.ReadAllText(Path.Combine(target, "quelle", "a.txt")));
        Assert.Equal("b", File.ReadAllText(Path.Combine(target, "quelle", "sub", "b.txt")));
        Assert.True(File.Exists(_temp.Combine("quelle/a.txt")));
    }

    [Fact]
    public void Copy_IntoItselfIsRejected()
    {
        var source = _temp.Dir("ordner");
        var child = _temp.Dir("ordner/kind");

        Assert.Throws<IOException>(() => _fs.Copy([source], child));
        Assert.Throws<IOException>(() => _fs.Move([source], source));
    }

    [Fact]
    public void Copy_SiblingWithCommonPrefixIsAllowed()
    {
        var source = _temp.Dir("ordner");
        var sibling = _temp.Dir("ordner2");

        _fs.Copy([source], sibling);

        Assert.True(Directory.Exists(Path.Combine(sibling, "ordner")));
    }

    [Fact]
    public void Move_MovesEntriesAndResolvesConflicts()
    {
        var file = _temp.File("a.txt", "neu");
        var dir = _temp.Dir("ordner");
        var target = _temp.Dir("ziel");
        File.WriteAllText(Path.Combine(target, "a.txt"), "alt");

        var moved = _fs.Move([file, dir], target);

        Assert.Equal([Path.Combine(target, "a (2).txt"), Path.Combine(target, "ordner")], moved);
        Assert.Equal("alt", File.ReadAllText(Path.Combine(target, "a.txt")));
        Assert.False(File.Exists(file));
        Assert.False(Directory.Exists(dir));
    }

    [Fact]
    public void Move_IntoSameFolderDoesNothing()
    {
        var file = _temp.File("a.txt");

        var moved = _fs.Move([file], _temp.Path);

        Assert.Equal([file], moved);
        Assert.True(File.Exists(file));
    }

    [Fact]
    public void DeletePermanently_RemovesFilesAndFolderTrees()
    {
        var file = _temp.File("a.txt");
        _temp.File("ordner/sub/b.txt");

        _fs.DeletePermanently([file, _temp.Combine("ordner")]);

        Assert.Empty(Directory.EnumerateFileSystemEntries(_temp.Path));
    }
}
