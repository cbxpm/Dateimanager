namespace Dateimanager.Core;

public enum ButtonActionKind
{
    BuiltIn,
    External,
}

/// <summary>Eingebaute Befehle, die ein Knopf auslösen kann.</summary>
public enum BuiltInCommand
{
    Copy,
    Move,
    NewFolder,
    Delete,
    DeletePermanently,
    Rename,
    SelectAll,
    SelectNone,
    InvertSelection,
    Parent,
    Root,
    Back,
    Forward,
    Refresh,
    SwapPanes,
    SyncPanes,
    ToggleHidden,
    Open,
    NextBank,
    PreviousBank,
}

/// <summary>Ein Knopf der Knopfleiste (wie in Directory Opus).</summary>
public sealed record ButtonDefinition
{
    public string Label { get; init; } = string.Empty;
    public ButtonActionKind Kind { get; init; }
    public BuiltInCommand Command { get; init; }

    /// <summary>Programm für externe Befehle (Name im PATH oder vollständiger Pfad).</summary>
    public string Program { get; init; } = string.Empty;

    /// <summary>Argumente mit Platzhaltern, siehe <see cref="CommandTemplate"/>.</summary>
    public string Arguments { get; init; } = string.Empty;

    /// <summary>Programm für jede ausgewählte Datei einzeln starten.</summary>
    public bool RunPerFile { get; init; }

    public string Background { get; init; } = "#AAAAAA";
    public string Foreground { get; init; } = "#000000";

    public static ButtonDefinition BuiltIn(string label, BuiltInCommand command, string background, string foreground) =>
        new() { Label = label, Kind = ButtonActionKind.BuiltIn, Command = command, Background = background, Foreground = foreground };

    public static ButtonDefinition External(string label, string program, string arguments, string background,
        string foreground, bool runPerFile = false) =>
        new()
        {
            Label = label,
            Kind = ButtonActionKind.External,
            Program = program,
            Arguments = arguments,
            RunPerFile = runPerFile,
            Background = background,
            Foreground = foreground,
        };
}

public static class BuiltInCommands
{
    public static readonly IReadOnlyDictionary<BuiltInCommand, string> DisplayNames = new Dictionary<BuiltInCommand, string>
    {
        [BuiltInCommand.Copy] = "Kopieren ins andere Fenster",
        [BuiltInCommand.Move] = "Verschieben ins andere Fenster",
        [BuiltInCommand.NewFolder] = "Neuer Ordner",
        [BuiltInCommand.Delete] = "In den Papierkorb",
        [BuiltInCommand.DeletePermanently] = "Endgültig löschen",
        [BuiltInCommand.Rename] = "Umbenennen",
        [BuiltInCommand.SelectAll] = "Alles auswählen",
        [BuiltInCommand.SelectNone] = "Auswahl aufheben",
        [BuiltInCommand.InvertSelection] = "Auswahl umkehren",
        [BuiltInCommand.Parent] = "Übergeordneter Ordner",
        [BuiltInCommand.Root] = "Wurzelverzeichnis",
        [BuiltInCommand.Back] = "Zurück",
        [BuiltInCommand.Forward] = "Vorwärts",
        [BuiltInCommand.Refresh] = "Aktualisieren",
        [BuiltInCommand.SwapPanes] = "Fenster tauschen",
        [BuiltInCommand.SyncPanes] = "Anderes Fenster gleichsetzen",
        [BuiltInCommand.ToggleHidden] = "Versteckte Dateien ein/aus",
        [BuiltInCommand.Open] = "Öffnen",
        [BuiltInCommand.NextBank] = "Nächste Knopfbank",
        [BuiltInCommand.PreviousBank] = "Vorherige Knopfbank",
    };
}

/// <summary>Eine Knopfbank: eine benannte Gruppe von Knöpfen, zwischen denen umgeschaltet werden kann.</summary>
public sealed class ButtonBankDefinition
{
    public string Name { get; set; } = "Bank";
    public int Columns { get; set; } = 8;
    public List<ButtonDefinition> Buttons { get; set; } = [];
}

public static class DefaultButtons
{
    private const string Blue = "#3B5BA5";
    private const string Red = "#A53B3B";
    private const string Grey = "#B4B4B4";
    private const string Green = "#3F7F4F";
    private const string Orange = "#E0A040";
    private const string White = "#FFFFFF";
    private const string Black = "#000000";

    public static List<ButtonBankDefinition> CreateBanks(Func<string, bool>? isOnPath = null)
    {
        isOnPath ??= IsOnPath;
        return
        [
            new ButtonBankDefinition { Name = "Standard", Columns = 8, Buttons = CreateStandard(isOnPath) },
            new ButtonBankDefinition { Name = "Extras", Columns = 8, Buttons = CreateExtras(isOnPath) },
        ];
    }

    private static List<ButtonDefinition> CreateStandard(Func<string, bool> isOnPath) =>
    [
        ButtonDefinition.BuiltIn("Kopieren", BuiltInCommand.Copy, Blue, White),
        ButtonDefinition.BuiltIn("Verschieben", BuiltInCommand.Move, Blue, White),
        ButtonDefinition.BuiltIn("Umbenennen", BuiltInCommand.Rename, Blue, White),
        ButtonDefinition.BuiltIn("Neuer Ordner", BuiltInCommand.NewFolder, Blue, White),
        ButtonDefinition.BuiltIn("Löschen", BuiltInCommand.Delete, Red, White),
        ButtonDefinition.BuiltIn("Alles", BuiltInCommand.SelectAll, Grey, Black),
        ButtonDefinition.BuiltIn("Nichts", BuiltInCommand.SelectNone, Grey, Black),
        ButtonDefinition.BuiltIn("Umkehren", BuiltInCommand.InvertSelection, Grey, Black),

        ButtonDefinition.BuiltIn("Eltern", BuiltInCommand.Parent, Green, White),
        ButtonDefinition.BuiltIn("Wurzel", BuiltInCommand.Root, Green, White),
        ButtonDefinition.BuiltIn("Tauschen", BuiltInCommand.SwapPanes, Green, White),
        ButtonDefinition.BuiltIn("Gleich", BuiltInCommand.SyncPanes, Green, White),
        ButtonDefinition.BuiltIn("Versteckt", BuiltInCommand.ToggleHidden, Grey, Black),
        ButtonDefinition.BuiltIn("Aktualisieren", BuiltInCommand.Refresh, Grey, Black),
        Terminal(isOnPath) with { Label = "Terminal", Background = Orange, Foreground = Black },
        Editor(isOnPath) with { Label = "Editor", Background = Orange, Foreground = Black },
    ];

    // tar ist unter Linux, macOS und Windows 10+ vorhanden und kann auch ZIP-Dateien entpacken
    private static List<ButtonDefinition> CreateExtras(Func<string, bool> isOnPath) =>
    [
        ButtonDefinition.BuiltIn("Zurück", BuiltInCommand.Back, Green, White),
        ButtonDefinition.BuiltIn("Vorwärts", BuiltInCommand.Forward, Green, White),
        ButtonDefinition.BuiltIn("Öffnen", BuiltInCommand.Open, Blue, White),
        ButtonDefinition.BuiltIn("Endgültig löschen", BuiltInCommand.DeletePermanently, Red, White),
        ButtonDefinition.External("Archivieren", "tar", "-czf \"{d}/{n}.tar.gz\" {o}", Orange, Black),
        ButtonDefinition.External("Entpacken", "tar", "-xf {s} -C {d}", Orange, Black, runPerFile: true),
        SystemExplorer() with { Label = "System-Explorer", Background = Orange, Foreground = Black },
        Terminal(isOnPath) with { Label = "Terminal", Background = Orange, Foreground = Black },
    ];

    private static ButtonDefinition SystemExplorer() =>
        OperatingSystem.IsWindows() ? ButtonDefinition.External("", "explorer.exe", "{p}", "", "")
        : OperatingSystem.IsMacOS() ? ButtonDefinition.External("", "open", "{p}", "", "")
        : ButtonDefinition.External("", "xdg-open", "{p}", "", "");

    // Arbeitsverzeichnis externer Programme ist immer der Ordner des aktiven Fensters
    private static ButtonDefinition Terminal(Func<string, bool> isOnPath)
    {
        if (OperatingSystem.IsWindows())
            return ButtonDefinition.External("", "cmd.exe", "", "", "");
        if (OperatingSystem.IsMacOS())
            return ButtonDefinition.External("", "open", "-a Terminal {p}", "", "");

        var terminal = new[] { "x-terminal-emulator", "gnome-terminal", "konsole", "xfce4-terminal", "xterm" }
            .FirstOrDefault(isOnPath) ?? "xterm";
        return ButtonDefinition.External("", terminal, "", "", "");
    }

    private static ButtonDefinition Editor(Func<string, bool> isOnPath)
    {
        if (OperatingSystem.IsWindows())
            return ButtonDefinition.External("", "notepad.exe", "{s}", "", "", runPerFile: true);
        if (OperatingSystem.IsMacOS())
            return ButtonDefinition.External("", "open", "-t {f}", "", "");

        var editor = new[] { "gnome-text-editor", "gedit", "kate", "mousepad", "xed", "pluma" }
            .FirstOrDefault(isOnPath);
        return editor is null
            ? ButtonDefinition.External("", "xdg-open", "{s}", "", "", runPerFile: true)
            : ButtonDefinition.External("", editor, "{f}", "", "");
    }

    private static bool IsOnPath(string program) =>
        (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
        .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
        .Any(dir => File.Exists(Path.Combine(dir, program)));
}
