using Dateimanager.Core;
using Dateimanager.Services;
using Dateimanager.ViewModels;

namespace Dateimanager.Tests;

internal sealed class FakeDialogService : IDialogService
{
    public Queue<string?> PromptAnswers { get; } = new();
    public Queue<ButtonDefinition?> ButtonAnswers { get; } = new();
    public bool ConfirmAnswer { get; set; } = true;
    public List<string> Errors { get; } = [];
    public List<string> Prompts { get; } = [];

    public Task<string?> PromptAsync(string title, string message, string initialText, Func<string, string?>? validate = null)
    {
        Prompts.Add(initialText);
        var answer = PromptAnswers.Dequeue();
        if (answer is not null && validate?.Invoke(answer) is { } error)
            throw new InvalidOperationException("Validierung fehlgeschlagen: " + error);
        return Task.FromResult(answer);
    }

    public Task<bool> ConfirmAsync(string title, string message, string confirmText) => Task.FromResult(ConfirmAnswer);

    public Task ShowErrorAsync(string title, string message)
    {
        Errors.Add(message);
        return Task.CompletedTask;
    }

    public Task<ButtonDefinition?> EditButtonAsync(string title, ButtonDefinition button) =>
        Task.FromResult(ButtonAnswers.Dequeue());
}

internal sealed class FakeTrash : ITrashService
{
    public List<string> Trashed { get; } = [];

    public void MoveToTrash(string path)
    {
        Trashed.Add(path);
        new FileSystemService().DeletePermanently([path]);
    }
}

internal sealed class FakeShell : IShellService
{
    public List<string> Opened { get; } = [];
    public List<(string Program, IReadOnlyList<string> Arguments, string WorkingDirectory)> Runs { get; } = [];

    public void Open(string path) => Opened.Add(path);

    public void Run(string program, IReadOnlyList<string> arguments, string workingDirectory) =>
        Runs.Add((program, arguments, workingDirectory));
}

/// <summary>Stellt ein MainWindowViewModel mit Fakes und temporären Ordnern bereit.</summary>
internal sealed class TestEnvironment : IDisposable
{
    public TestEnvironment()
    {
        Settings = new AppSettings { Buttons = DefaultButtons.Create(_ => false) };
        Store = new SettingsStore(Temp.Combine("config/settings.json"));
        ViewModel = new MainWindowViewModel(new FileSystemService(), Trash, Dialogs, Shell, Store, Settings,
            [new Location("Test", Temp.Path, LocationKind.Folder)]);
    }

    public TempDirectory Temp { get; } = new();
    public FakeDialogService Dialogs { get; } = new();
    public FakeTrash Trash { get; } = new();
    public FakeShell Shell { get; } = new();
    public AppSettings Settings { get; }
    public SettingsStore Store { get; }
    public MainWindowViewModel ViewModel { get; }

    public PaneViewModel Left => ViewModel.Left;
    public PaneViewModel Right => ViewModel.Right;

    public static string[] Names(PaneViewModel pane) => pane.Entries.Select(e => e.Name).ToArray();

    public static void Select(PaneViewModel pane, params string[] names) =>
        pane.SetSelection(pane.Entries.Where(e => names.Contains(e.Name)));

    public void Dispose()
    {
        ViewModel.Dispose();
        Temp.Dispose();
    }
}
