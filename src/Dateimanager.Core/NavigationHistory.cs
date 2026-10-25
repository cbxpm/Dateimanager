namespace Dateimanager.Core;

/// <summary>Zurück-/Vorwärts-Verlauf wie in einem Webbrowser.</summary>
public sealed class NavigationHistory
{
    private readonly Stack<string> _back = new();
    private readonly Stack<string> _forward = new();

    public string? Current { get; private set; }
    public bool CanGoBack => _back.Count > 0;
    public bool CanGoForward => _forward.Count > 0;

    public void Navigate(string path)
    {
        if (path == Current)
            return;
        if (Current is not null)
            _back.Push(Current);
        _forward.Clear();
        Current = path;
    }

    public string? PeekBack() => CanGoBack ? _back.Peek() : null;
    public string? PeekForward() => CanGoForward ? _forward.Peek() : null;

    public string? GoBack()
    {
        if (!CanGoBack)
            return null;
        _forward.Push(Current!);
        return Current = _back.Pop();
    }

    public string? GoForward()
    {
        if (!CanGoForward)
            return null;
        _back.Push(Current!);
        return Current = _forward.Pop();
    }
}
