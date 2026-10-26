using Dateimanager.Core;

namespace Dateimanager.Services;

public interface IDialogService
{
    /// <summary>Fragt einen Text ab; liefert null bei Abbruch.</summary>
    Task<string?> PromptAsync(string title, string message, string initialText, Func<string, string?>? validate = null);

    Task<bool> ConfirmAsync(string title, string message, string confirmText);

    Task ShowErrorAsync(string title, string message);

    /// <summary>Öffnet den Knopf-Editor; liefert null bei Abbruch.</summary>
    Task<ButtonDefinition?> EditButtonAsync(string title, ButtonDefinition button);

    /// <summary>Öffnet den Dialog „Schriftart und Zeilenabstand“; liefert null bei Abbruch.</summary>
    Task<AppearanceSettings?> EditAppearanceAsync(AppearanceSettings settings);
}
