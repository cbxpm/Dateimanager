using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Dateimanager.Core;
using Dateimanager.ViewModels;

namespace Dateimanager.Tests;

public sealed class ButtonBarTests : IDisposable
{
    private readonly TestEnvironment _env = new();
    private ButtonBarViewModel Bar => _env.ViewModel.ButtonBar;
    private ButtonBankViewModel Bank => Bar.CurrentBank;

    public void Dispose() => _env.Dispose();

    private AppSettings Saved() => _env.Store.Load();

    private IReadOnlyList<string> SavedLabels(int bank = 0) => Saved().Banks[bank].Buttons.Select(b => b.Label).ToList();

    [AvaloniaFact]
    public void Defaults_HaveStandardAndExtrasBank()
    {
        Assert.Equal(["Standard", "Extras"], Bar.Banks.Select(b => b.Name));
        Assert.Same(Bar.Banks[0], Bank);
        Assert.True(Bank.IsCurrent);
        Assert.Equal("1/2", Bar.BankPosition);
        Assert.Equal(16, Bank.Buttons.Count);
        Assert.Equal(8, Bank.Columns);

        var copy = Bank.Buttons[0];
        Assert.Equal("Kopieren", copy.Label);
        Assert.Equal(Color.Parse("#3B5BA5"), ((ISolidColorBrush)copy.Background).Color);
        Assert.Equal("Kopieren ins andere Fenster", copy.ToolTip);
    }

    [AvaloniaFact]
    public void NextAndPreviousBank_CycleAndPersistActiveBank()
    {
        Bar.NextBankCommand.Execute(null);
        Assert.Equal("Extras", Bank.Name);
        Assert.False(Bar.Banks[0].IsCurrent);
        Assert.Equal("2/2", Bar.BankPosition);
        Assert.Equal(1, Saved().ActiveBank);

        Bar.NextBankCommand.Execute(null); // wieder von vorn
        Assert.Equal("Standard", Bank.Name);

        Bar.PreviousBankCommand.Execute(null);
        Assert.Equal("Extras", Bank.Name);

        Bar.Banks[0].SelectCommand.Execute(null);
        Assert.Equal("Standard", Bank.Name);
    }

    [AvaloniaFact]
    public async Task BuiltInButton_CanSwitchBanks()
    {
        await _env.ViewModel.ExecuteBuiltInAsync(BuiltInCommand.NextBank);

        Assert.Equal("Extras", Bank.Name);
    }

    [AvaloniaFact]
    public async Task AddBank_InsertsAfterCurrentAndSelectsIt()
    {
        _env.Dialogs.PromptAnswers.Enqueue("Bilder");

        await Bar.AddBankCommand.ExecuteAsync(null);

        Assert.Equal(["Standard", "Bilder", "Extras"], Bar.Banks.Select(b => b.Name));
        Assert.Equal("Bilder", Bank.Name);
        Assert.True(Bank.IsEmpty);
        Assert.Equal("2/3", Bar.BankPosition);
        Assert.Equal(["Standard", "Bilder", "Extras"], Saved().Banks.Select(b => b.Name));
        Assert.Equal(1, Saved().ActiveBank);
    }

    [AvaloniaFact]
    public async Task RenameDuplicateAndReorderBanks()
    {
        _env.Dialogs.PromptAnswers.Enqueue("Dateien");
        await Bar.RenameBankCommand.ExecuteAsync(null);
        Assert.Equal("Dateien", Bar.Banks[0].Name);

        Bar.DuplicateBankCommand.Execute(null);
        Assert.Equal(["Dateien", "Dateien (Kopie)", "Extras"], Bar.Banks.Select(b => b.Name));
        Assert.Equal(16, Bank.Buttons.Count);
        Assert.NotSame(Bar.Banks[0].Buttons[0], Bank.Buttons[0]);

        Bar.MoveBankRightCommand.Execute(null);
        Assert.Equal(["Dateien", "Extras", "Dateien (Kopie)"], Saved().Banks.Select(b => b.Name));
        Assert.Equal("3/3", Bar.BankPosition);

        Bar.MoveBankRightCommand.Execute(null); // schon am Ende
        Assert.Equal("3/3", Bar.BankPosition);
    }

    [AvaloniaFact]
    public async Task RemoveBank_KeepsAtLeastOne()
    {
        Bar.NextBankCommand.Execute(null);
        await Bar.RemoveBankCommand.ExecuteAsync(null);

        Assert.Equal(["Standard"], Bar.Banks.Select(b => b.Name));
        Assert.Equal("Standard", Bank.Name);
        Assert.False(Bar.RemoveBankCommand.CanExecute(null));
        Assert.Equal(0, Saved().ActiveBank);
    }

    [AvaloniaFact]
    public void ColumnsArePerBank()
    {
        Bar.SetColumnsCommand.Execute(6);

        Assert.Equal(6, Saved().Banks[0].Columns);
        Assert.Equal(8, Saved().Banks[1].Columns);
    }

    [AvaloniaFact]
    public void Rows_FollowLargestBank()
    {
        Assert.Equal(2, Bar.Rows); // 16 Knöpfe in 8 Spalten

        Bar.SetColumnsCommand.Execute(4);
        Assert.Equal(4, Bar.Rows);

        Bar.NextBankCommand.Execute(null);
        Assert.Equal(4, Bar.Rows); // auch wenn die kleinere Bank angezeigt wird
    }

    [AvaloniaFact]
    public void MoveButtonToOtherBank()
    {
        var copy = Bank.Buttons[0];
        var target = Assert.Single(copy.MoveTargets);
        Assert.Equal("Extras", target.Name);

        target.MoveCommand.Execute(null);

        Assert.Equal(15, Bank.Buttons.Count);
        Assert.Equal("Kopieren", Bar.Banks[1].Buttons[^1].Label);
        Assert.Same(Bar.Banks[1], Bar.Banks[1].Buttons[^1].Bank);
        Assert.Equal("Kopieren", SavedLabels(1)[^1]);
    }

    [AvaloniaFact]
    public async Task MoveTargets_FollowBankChanges()
    {
        var button = Bank.Buttons[0];
        _env.Dialogs.PromptAnswers.Enqueue("Neu");
        await Bar.AddBankCommand.ExecuteAsync(null);

        Assert.Equal(["Neu", "Extras"], button.MoveTargets.Select(t => t.Name));
    }

    [AvaloniaFact]
    public async Task EditButton_ReplacesButtonAndSaves()
    {
        _env.Dialogs.ButtonAnswers.Enqueue(ButtonDefinition.External("Gimp", "gimp", "{f}", "#8855AA", "#FFFFFF"));

        await Bank.Buttons[0].EditCommand.ExecuteAsync(null);

        Assert.Equal("Gimp", Bank.Buttons[0].Label);
        Assert.Equal("Gimp", SavedLabels()[0]);
        Assert.Equal(ButtonActionKind.External, Saved().Banks[0].Buttons[0].Kind);
    }

    [AvaloniaFact]
    public async Task EditButton_CancelKeepsButton()
    {
        _env.Dialogs.ButtonAnswers.Enqueue(null);

        await Bank.Buttons[0].EditCommand.ExecuteAsync(null);

        Assert.Equal("Kopieren", Bank.Buttons[0].Label);
        Assert.False(File.Exists(_env.Store.FilePath));
    }

    [AvaloniaFact]
    public async Task AddButton_InCurrentBankOrAfterButton()
    {
        _env.Dialogs.ButtonAnswers.Enqueue(ButtonDefinition.BuiltIn("Wurzel 2", BuiltInCommand.Root, "#000", "#fff"));
        await Bank.Buttons[1].AddAfterCommand.ExecuteAsync(null);
        Assert.Equal(["Kopieren", "Verschieben", "Wurzel 2", "Umbenennen"], Bank.Buttons.Take(4).Select(b => b.Label));

        Bar.NextBankCommand.Execute(null);
        _env.Dialogs.ButtonAnswers.Enqueue(ButtonDefinition.BuiltIn("Ende", BuiltInCommand.Root, "#000", "#fff"));
        await Bar.AddButtonCommand.ExecuteAsync(null);
        Assert.Equal("Ende", SavedLabels(1)[^1]);
    }

    [AvaloniaFact]
    public void MoveAndDuplicateButtons_ArePersisted()
    {
        Bank.Buttons[0].MoveRightCommand.Execute(null);
        Assert.Equal(["Verschieben", "Kopieren"], SavedLabels().Take(2));

        Bank.Buttons[0].MoveLeftCommand.Execute(null); // am Anfang: nichts passiert
        Bank.Buttons[1].DuplicateCommand.Execute(null);
        Assert.Equal(["Verschieben", "Kopieren", "Kopieren"], SavedLabels().Take(3));
    }

    [AvaloniaFact]
    public async Task RemoveButton_AfterConfirmation()
    {
        await Bank.Buttons[0].RemoveCommand.ExecuteAsync(null);

        Assert.Equal(15, Bank.Buttons.Count);
        Assert.DoesNotContain("Kopieren", SavedLabels());
    }

    [AvaloniaFact]
    public async Task ResetToDefaults_RestoresBothBanks()
    {
        Bar.NextBankCommand.Execute(null);
        await Bar.RemoveBankCommand.ExecuteAsync(null);

        await Bar.ResetToDefaultsCommand.ExecuteAsync(null);

        Assert.Equal(["Standard", "Extras"], Bar.Banks.Select(b => b.Name));
        Assert.Equal("Standard", Bank.Name);
    }

    [AvaloniaFact]
    public async Task BuiltInButton_ExecutesCommand()
    {
        var sub = _env.Temp.Dir("sub");
        await _env.ViewModel.InitializeAsync(sub, sub);

        await Bank.Buttons.Single(b => b.Label == "Eltern").ExecuteCommand.ExecuteAsync(null);

        Assert.Equal(_env.Temp.Path, _env.Left.CurrentPath);
    }

    [AvaloniaFact]
    public async Task ExtrasBank_ArchiveUsesNamesRelativeToSourceFolder()
    {
        _env.Temp.File("quelle/a.txt");
        _env.Temp.File("quelle/b.txt");
        var target = _env.Temp.Dir("ziel");
        await _env.ViewModel.InitializeAsync(_env.Temp.Combine("quelle"), target);
        TestEnvironment.Select(_env.Left, "a.txt", "b.txt");
        Bar.NextBankCommand.Execute(null);

        await Bank.Buttons.Single(b => b.Label == "Archivieren").ExecuteCommand.ExecuteAsync(null);

        var run = Assert.Single(_env.Shell.Runs);
        Assert.Equal("tar", run.Program);
        Assert.Equal(["-czf", $"{target}/a.txt.tar.gz", "a.txt", "b.txt"], run.Arguments);
        Assert.Equal(_env.Temp.Combine("quelle"), run.WorkingDirectory);
    }

    [AvaloniaFact]
    public void Editor_ValidatesAndBuildsDefinition()
    {
        var editor = new ButtonEditorViewModel(ButtonDefinition.BuiltIn("Alt", BuiltInCommand.Copy, "#111111", "#EEEEEE"));
        Assert.True(editor.IsBuiltIn);

        editor.IsExternal = true;
        Assert.Null(editor.BuildResult());
        Assert.Contains("Programm", editor.Error);

        editor.Program = "gimp";
        editor.Arguments = "{f}";
        editor.SetBackgroundCommand.Execute("#8855AA");
        editor.ForegroundColor = "kaputt";
        Assert.Null(editor.BuildResult());

        editor.ForegroundColor = "#FFFFFF";
        var result = editor.BuildResult();
        Assert.Equal(ButtonDefinition.External("Alt", "gimp", "{f}", "#8855AA", "#FFFFFF") with { Command = BuiltInCommand.Copy }, result);
    }
}
