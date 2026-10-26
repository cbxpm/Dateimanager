using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Dateimanager.Core;
using Dateimanager.Views;

namespace Dateimanager.Services;

/// <summary>Einfache modale Dialoge, im Code aufgebaut.</summary>
public sealed class DialogService(Window owner) : IDialogService
{
    public async Task<string?> PromptAsync(string title, string message, string initialText,
        Func<string, string?>? validate = null)
    {
        var input = new TextBox { Text = initialText, MinWidth = 360 };
        var error = new TextBlock
        {
            Foreground = Brushes.IndianRed,
            TextWrapping = TextWrapping.Wrap,
            IsVisible = false,
        };

        var dialog = CreateDialog(title, message, out var buttons, input, error);
        var ok = AddButton(buttons, "OK", isDefault: true);
        AddButton(buttons, "Abbrechen", isCancel: true).Click += (_, _) => dialog.Close(null);

        ok.Click += (_, _) =>
        {
            var text = input.Text?.Trim() ?? string.Empty;
            if (validate?.Invoke(text) is { } validationError)
            {
                error.Text = validationError;
                error.IsVisible = true;
                return;
            }

            dialog.Close(text);
        };

        dialog.Opened += (_, _) =>
        {
            input.Focus();
            // Bei Dateinamen nur den Namen ohne Endung markieren, Pfade komplett
            var isPath = initialText.IndexOfAny(['/', '\\']) >= 0;
            var dot = initialText.LastIndexOf('.');
            input.SelectionStart = 0;
            input.SelectionEnd = !isPath && dot > 0 ? dot : initialText.Length;
        };

        return await dialog.ShowDialog<string?>(owner);
    }

    public async Task<bool> ConfirmAsync(string title, string message, string confirmText)
    {
        var dialog = CreateDialog(title, message, out var buttons);
        AddButton(buttons, confirmText, isDefault: true).Click += (_, _) => dialog.Close(true);
        var cancel = AddButton(buttons, "Abbrechen", isCancel: true);
        cancel.Click += (_, _) => dialog.Close(false);
        dialog.Opened += (_, _) => cancel.Focus();

        return await dialog.ShowDialog<bool>(owner);
    }

    public async Task ShowErrorAsync(string title, string message)
    {
        var dialog = CreateDialog(title, message, out var buttons);
        AddButton(buttons, "OK", isDefault: true, isCancel: true).Click += (_, _) => dialog.Close();
        await dialog.ShowDialog(owner);
    }

    public Task<ButtonDefinition?> EditButtonAsync(string title, ButtonDefinition button) =>
        ButtonEditorWindow.ShowAsync(owner, title, button);

    public Task<AppearanceSettings?> EditAppearanceAsync(AppearanceSettings settings) =>
        AppearanceWindow.ShowAsync(owner, settings);

    private static Window CreateDialog(string title, string message, out StackPanel buttons,
        params Control[] content)
    {
        buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8,
            Margin = new Thickness(0, 8, 0, 0),
        };

        var panel = new StackPanel { Spacing = 10, Margin = new Thickness(20) };
        panel.Children.Add(new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap, MaxWidth = 480 });
        panel.Children.AddRange(content);
        panel.Children.Add(buttons);

        var window = new Window
        {
            Title = title,
            Content = panel,
            SizeToContent = SizeToContent.WidthAndHeight,
            CanResize = false,
            ShowInTaskbar = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            MinWidth = 320,
        };
        return window;
    }

    private static Button AddButton(StackPanel buttons, string text, bool isDefault = false, bool isCancel = false)
    {
        var button = new Button
        {
            Content = text,
            MinWidth = 90,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            IsDefault = isDefault,
            IsCancel = isCancel,
        };
        if (isDefault)
            button.Classes.Add("accent");
        buttons.Children.Add(button);
        return button;
    }
}
