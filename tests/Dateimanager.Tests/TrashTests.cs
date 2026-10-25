using Dateimanager.Core;

namespace Dateimanager.Tests;

public sealed class TrashTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void FreedesktopTrash_MovesFileAndWritesTrashInfo()
    {
        var trash = new FreedesktopTrash(_temp.Combine("Trash"));
        var file = _temp.File("Mein Bericht.txt", "x");

        trash.MoveToTrash(file);

        Assert.False(File.Exists(file));
        Assert.Equal("x", File.ReadAllText(_temp.Combine("Trash/files/Mein Bericht.txt")));
        var info = File.ReadAllText(_temp.Combine("Trash/info/Mein Bericht.txt.trashinfo"));
        Assert.StartsWith("[Trash Info]\n", info);
        Assert.Contains($"Path={FreedesktopTrash.EncodePath(file)}\n", info);
        Assert.Matches(@"DeletionDate=\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}", info);
    }

    [Fact]
    public void FreedesktopTrash_AvoidsNameCollisions()
    {
        var trash = new FreedesktopTrash(_temp.Combine("Trash"));

        trash.MoveToTrash(_temp.File("a/doc.txt", "1"));
        trash.MoveToTrash(_temp.File("b/doc.txt", "2"));
        trash.MoveToTrash(_temp.Dir("c/doc.txt"));

        Assert.Equal("1", File.ReadAllText(_temp.Combine("Trash/files/doc.txt")));
        Assert.Equal("2", File.ReadAllText(_temp.Combine("Trash/files/doc.2.txt")));
        Assert.True(Directory.Exists(_temp.Combine("Trash/files/doc.3.txt")));
        Assert.Equal(3, Directory.GetFiles(_temp.Combine("Trash/info")).Length);
    }

    [Fact]
    public void FreedesktopTrash_EncodesPathAsUri()
    {
        Assert.Equal("/home/user/Gr%C3%B6%C3%9Fe%20und%20%25.txt",
            FreedesktopTrash.EncodePath("/home/user/Größe und %.txt"));
    }

    [Fact]
    public void MacTrash_MovesFolderWithUniqueName()
    {
        var trashDir = _temp.Dir(".Trash");
        _temp.Dir(".Trash/ordner");
        var trash = new MacTrash(trashDir);

        trash.MoveToTrash(_temp.Dir("ordner"));

        Assert.True(Directory.Exists(Path.Combine(trashDir, "ordner (2)")));
        Assert.False(Directory.Exists(_temp.Combine("ordner")));
    }
}
