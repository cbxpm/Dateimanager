namespace Dateimanager.Services;

public interface IDialogService
{
    /// <summary>Fragt einen Text ab; liefert null bei Abbruch.</summary>
    Task<string?> PromptAsync(string title, string message, string initialText, Func<string, string?>? validate = null);

    Task<bool> ConfirmAsync(string title, string message, string confirmText);

    Task ShowErrorAsync(string title, string message);
}
