using Dateimanager.Core;
using Dateimanager.Services;

namespace Dateimanager.Tests;

internal sealed class FakeDialogService : IDialogService
{
    public Queue<string?> PromptAnswers { get; } = new();
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

internal sealed class FakeLauncher : IFileLauncher
{
    public List<string> Opened { get; } = [];
    public void Open(string path) => Opened.Add(path);
}
