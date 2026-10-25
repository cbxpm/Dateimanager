using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Dateimanager.Core;
using Dateimanager.ViewModels;

namespace Dateimanager.Tests;

public sealed class ButtonBankTests : IDisposable
{
    private readonly TestEnvironment _env = new();
    private ButtonBankViewModel Bank => _env.ViewModel.ButtonBank;

    public void Dispose() => _env.Dispose();

    private IReadOnlyList<string> SavedLabels() => _env.Store.Load().Buttons.Select(b => b.Label).ToList();

    [AvaloniaFact]
    public void DefaultBank_HasSixteenColoredButtons()
    {
        Assert.Equal(16, Bank.Buttons.Count);
        Assert.Equal(8, Bank.Columns);
        var copy = Bank.Buttons[0];
        Assert.Equal("Kopieren", copy.Label);
        Assert.Equal(Color.Parse("#3B5BA5"), ((ISolidColorBrush)copy.Background!).Color);
        Assert.Equal("Kopieren ins andere Fenster", copy.ToolTip);
    }

    [AvaloniaFact]
    public async Task Edit_ReplacesButtonAndSaves()
    {
        _env.Dialogs.ButtonAnswers.Enqueue(ButtonDefinition.External("Gimp", "gimp", "{f}", "#8855AA", "#FFFFFF"));

        await Bank.Buttons[0].EditCommand.ExecuteAsync(null);

        Assert.Equal("Gimp", Bank.Buttons[0].Label);
        Assert.Equal("Gimp", SavedLabels()[0]);
        Assert.Equal(ButtonActionKind.External, _env.Store.Load().Buttons[0].Kind);
    }

    [AvaloniaFact]
    public async Task Edit_CancelKeepsButton()
    {
        _env.Dialogs.ButtonAnswers.Enqueue(null);

        await Bank.Buttons[0].EditCommand.ExecuteAsync(null);

        Assert.Equal("Kopieren", Bank.Buttons[0].Label);
        Assert.False(File.Exists(_env.Store.FilePath));
    }

    [AvaloniaFact]
    public async Task AddAfter_InsertsBehindButton()
    {
        _env.Dialogs.ButtonAnswers.Enqueue(ButtonDefinition.BuiltIn("Wurzel 2", BuiltInCommand.Root, "#000", "#fff"));

        await Bank.Buttons[1].AddAfterCommand.ExecuteAsync(null);

        Assert.Equal(["Kopieren", "Verschieben", "Wurzel 2", "Umbenennen"], Bank.Buttons.Take(4).Select(b => b.Label));
    }

    [AvaloniaFact]
    public void MoveDuplicateAndColumns_ArePersisted()
    {
        Bank.Buttons[0].MoveRightCommand.Execute(null);
        Assert.Equal(["Verschieben", "Kopieren"], SavedLabels().Take(2));

        Bank.Buttons[0].MoveLeftCommand.Execute(null); // am Anfang: nichts passiert
        Bank.Buttons[1].DuplicateCommand.Execute(null);
        Assert.Equal(["Verschieben", "Kopieren", "Kopieren"], SavedLabels().Take(3));

        Bank.SetColumnsCommand.Execute(6);
        Assert.Equal(6, _env.Store.Load().ButtonColumns);
    }

    [AvaloniaFact]
    public async Task Remove_AfterConfirmation()
    {
        await Bank.Buttons[0].RemoveCommand.ExecuteAsync(null);

        Assert.Equal(15, Bank.Buttons.Count);
        Assert.DoesNotContain("Kopieren", SavedLabels());
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
