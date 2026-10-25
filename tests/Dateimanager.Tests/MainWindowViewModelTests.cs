using Avalonia.Headless.XUnit;
using Dateimanager.Core;
using static Dateimanager.Tests.TestEnvironment;

namespace Dateimanager.Tests;

public sealed class MainWindowViewModelTests : IDisposable
{
    private readonly TestEnvironment _env = new();
    private TempDirectory Temp => _env.Temp;

    public void Dispose() => _env.Dispose();

    private async Task OpenAsync(string left, string right) => await _env.ViewModel.InitializeAsync(left, right);

    [AvaloniaFact]
    public async Task ActivePane_FollowsActivationAndTab()
    {
        await OpenAsync(Temp.Path, Temp.Path);
        Assert.True(_env.Left.IsActive);

        _env.Right.RequestActivation();
        Assert.Same(_env.Right, _env.ViewModel.ActivePane);
        Assert.False(_env.Left.IsActive);

        _env.ViewModel.SwitchPaneCommand.Execute(null);
        Assert.Same(_env.Left, _env.ViewModel.ActivePane);
        Assert.Same(_env.Right, _env.ViewModel.InactivePane);
    }

    [AvaloniaFact]
    public async Task CopyToOther_CopiesIntoInactivePaneFolder()
    {
        Temp.File("quelle/a.txt", "x");
        var target = Temp.Dir("ziel");
        await OpenAsync(Temp.Combine("quelle"), target);
        Select(_env.Left, "a.txt");
        _env.Dialogs.PromptAnswers.Enqueue(target);

        await _env.ViewModel.CopyToOtherCommand.ExecuteAsync(null);

        Assert.Equal(target, _env.Dialogs.Prompts.Single());
        Assert.Equal(["a.txt"], Names(_env.Right));
        Assert.Equal("a.txt", _env.Right.SelectedEntry?.Name);
        Assert.True(File.Exists(Temp.Combine("quelle/a.txt")));
    }

    [AvaloniaFact]
    public async Task MoveToOther_FromRightPane()
    {
        var left = Temp.Dir("links");
        Temp.File("rechts/a.txt");
        Temp.File("rechts/b.txt");
        await OpenAsync(left, Temp.Combine("rechts"));
        _env.Right.RequestActivation();
        Select(_env.Right, "a.txt", "b.txt");
        _env.Dialogs.PromptAnswers.Enqueue(left);

        await _env.ViewModel.ExecuteBuiltInAsync(BuiltInCommand.Move);

        Assert.Equal(["a.txt", "b.txt"], Names(_env.Left));
        Assert.Empty(_env.Right.Entries);
    }

    [AvaloniaFact]
    public async Task CopyToOther_IsDisabledWithoutSelection()
    {
        await OpenAsync(Temp.Path, Temp.Path);

        Assert.False(_env.ViewModel.CopyToOtherCommand.CanExecute(null));
        Assert.False(_env.ViewModel.RenameCommand.CanExecute(null));
    }

    [AvaloniaFact]
    public async Task NewFolder_InActivePaneAndRefreshesOtherPaneShowingSameFolder()
    {
        Temp.Dir("Neuer Ordner");
        await OpenAsync(Temp.Path, Temp.Path);
        _env.Dialogs.PromptAnswers.Enqueue("Projekte");

        await _env.ViewModel.NewFolderCommand.ExecuteAsync(null);

        Assert.Equal("Neuer Ordner (2)", _env.Dialogs.Prompts.Single());
        Assert.Equal("Projekte", _env.Left.SelectedEntry?.Name);
        Assert.Contains("Projekte", Names(_env.Right));
    }

    [AvaloniaFact]
    public async Task Rename_RequiresSingleSelection()
    {
        Temp.File("a.txt");
        Temp.File("b.txt");
        await OpenAsync(Temp.Path, Temp.Path);

        Select(_env.Left, "a.txt", "b.txt");
        Assert.False(_env.ViewModel.RenameCommand.CanExecute(null));

        Select(_env.Left, "a.txt");
        _env.Dialogs.PromptAnswers.Enqueue("c.txt");
        await _env.ViewModel.RenameCommand.ExecuteAsync(null);

        Assert.Equal(["b.txt", "c.txt"], Names(_env.Left));
        Assert.Equal("c.txt", _env.Left.SelectedEntry?.Name);
    }

    [AvaloniaFact]
    public async Task MoveToTrash_OnlyAfterConfirmation()
    {
        var file = Temp.File("weg.txt");
        await OpenAsync(Temp.Path, Temp.Path);
        Select(_env.Left, "weg.txt");

        _env.Dialogs.ConfirmAnswer = false;
        await _env.ViewModel.MoveToTrashCommand.ExecuteAsync(null);
        Assert.Empty(_env.Trash.Trashed);

        _env.Dialogs.ConfirmAnswer = true;
        await _env.ViewModel.MoveToTrashCommand.ExecuteAsync(null);
        Assert.Equal([file], _env.Trash.Trashed);
        Assert.Empty(_env.Left.Entries);
        Assert.Empty(_env.Right.Entries);
    }

    [AvaloniaFact]
    public async Task ClipboardCutAndPaste_MovesIntoActivePane()
    {
        Temp.File("a.txt", "x");
        var target = Temp.Dir("ziel");
        await OpenAsync(Temp.Path, target);

        Select(_env.Left, "a.txt");
        _env.ViewModel.ClipboardCutCommand.Execute(null);
        _env.Right.RequestActivation();
        await _env.ViewModel.PasteCommand.ExecuteAsync(null);

        Assert.Equal(["a.txt"], Names(_env.Right));
        Assert.False(File.Exists(Temp.Combine("a.txt")));
        Assert.False(_env.ViewModel.PasteCommand.CanExecute(null));
    }

    [AvaloniaFact]
    public async Task SwapAndSyncPanes()
    {
        var a = Temp.Dir("a");
        var b = Temp.Dir("b");
        await OpenAsync(a, b);

        await _env.ViewModel.ExecuteBuiltInAsync(BuiltInCommand.SwapPanes);
        Assert.Equal(b, _env.Left.CurrentPath);
        Assert.Equal(a, _env.Right.CurrentPath);

        await _env.ViewModel.ExecuteBuiltInAsync(BuiltInCommand.SyncPanes);
        Assert.Equal(b, _env.Right.CurrentPath);
    }

    [AvaloniaFact]
    public async Task ToggleHidden_AppliesToBothPanes()
    {
        Temp.File(".versteckt");
        if (OperatingSystem.IsWindows())
            File.SetAttributes(Temp.Combine(".versteckt"), FileAttributes.Hidden);
        await OpenAsync(Temp.Path, Temp.Path);
        Assert.Empty(_env.Left.Entries);

        await _env.ViewModel.ExecuteBuiltInAsync(BuiltInCommand.ToggleHidden);
        await _env.ViewModel.RefreshCommand.ExecuteAsync(null);

        Assert.Equal([".versteckt"], Names(_env.Left));
        Assert.Equal([".versteckt"], Names(_env.Right));
        Assert.True(_env.Settings.ShowHidden);
    }

    [AvaloniaFact]
    public async Task ExternalButton_ExpandsPlaceholders()
    {
        var a = Temp.File("links/a b.txt");
        var c = Temp.File("links/c.txt");
        var right = Temp.Dir("rechts");
        await OpenAsync(Temp.Combine("links"), right);
        Select(_env.Left, "a b.txt", "c.txt");

        await _env.ViewModel.ExecuteButtonAsync(
            ButtonDefinition.External("Packen", "tar", "-czf {d}/archiv.tgz {f}", "#000", "#fff"));

        var run = Assert.Single(_env.Shell.Runs);
        Assert.Equal("tar", run.Program);
        Assert.Equal(["-czf", $"{right}/archiv.tgz", a, c], run.Arguments);
        Assert.Equal(Temp.Combine("links"), run.WorkingDirectory);
    }

    [AvaloniaFact]
    public async Task ExternalButton_RunPerFile()
    {
        Temp.File("a.txt");
        Temp.File("b.txt");
        await OpenAsync(Temp.Path, Temp.Path);
        Select(_env.Left, "a.txt", "b.txt");

        await _env.ViewModel.ExecuteButtonAsync(
            ButtonDefinition.External("Ansehen", "viewer", "--name {n}", "#000", "#fff", runPerFile: true));

        Assert.Equal([["--name", "a.txt"], ["--name", "b.txt"]], _env.Shell.Runs.Select(r => r.Arguments.ToArray()));
    }

    [AvaloniaFact]
    public async Task ExternalButton_NeedingSelectionShowsErrorWithoutSelection()
    {
        await OpenAsync(Temp.Path, Temp.Path);

        await _env.ViewModel.ExecuteButtonAsync(ButtonDefinition.External("Editor", "edit", "{f}", "#000", "#fff"));

        Assert.Empty(_env.Shell.Runs);
        Assert.Single(_env.Dialogs.Errors);
    }

    [AvaloniaFact]
    public async Task SaveSettings_StoresPanePaths()
    {
        var a = Temp.Dir("a");
        var b = Temp.Dir("b");
        await OpenAsync(a, b);

        _env.ViewModel.SaveSettings();

        var loaded = _env.Store.Load();
        Assert.Equal(a, loaded.LeftPath);
        Assert.Equal(b, loaded.RightPath);
    }
}
