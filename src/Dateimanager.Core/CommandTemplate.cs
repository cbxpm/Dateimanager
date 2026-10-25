using System.Text;

namespace Dateimanager.Core;

/// <summary>Kontext, in dem ein externer Befehl ausgeführt wird.</summary>
public sealed record CommandContext(
    string SourceDirectory,
    string DestinationDirectory,
    IReadOnlyList<string> SelectedPaths);

/// <summary>
/// Wandelt eine Argumentvorlage in eine Argumentliste um.
/// <list type="bullet">
/// <item><c>{f}</c> – alle ausgewählten Pfade (als eigenes Argument: ein Argument je Datei)</item>
/// <item><c>{s}</c> – erster ausgewählter Pfad</item>
/// <item><c>{n}</c> – Name des ersten ausgewählten Eintrags</item>
/// <item><c>{o}</c> – Namen aller ausgewählten Einträge ohne Pfad (wie <c>{f}</c>)</item>
/// <item><c>{p}</c> – Ordner des aktiven Fensters (Quelle)</item>
/// <item><c>{d}</c> – Ordner des anderen Fensters (Ziel)</item>
/// </list>
/// Argumente mit Leerzeichen werden in doppelte Anführungszeichen gesetzt, <c>""</c> steht darin für ein Anführungszeichen.
/// </summary>
public static class CommandTemplate
{
    private static readonly string[] SelectionPlaceholders = ["{f}", "{o}", "{s}", "{n}"];

    public static bool UsesSelection(string template) =>
        SelectionPlaceholders.Any(p => template.Contains(p, StringComparison.Ordinal));

    public static IReadOnlyList<string> Expand(string template, CommandContext context)
    {
        var first = context.SelectedPaths.FirstOrDefault() ?? string.Empty;
        var names = context.SelectedPaths.Select(NameOf).ToList();
        var result = new List<string>();

        foreach (var token in Tokenize(template))
        {
            switch (token)
            {
                case "{f}":
                    result.AddRange(context.SelectedPaths);
                    continue;
                case "{o}":
                    result.AddRange(names);
                    continue;
            }

            result.Add(token
                .Replace("{f}", Quoted(context.SelectedPaths), StringComparison.Ordinal)
                .Replace("{o}", Quoted(names), StringComparison.Ordinal)
                .Replace("{s}", first, StringComparison.Ordinal)
                .Replace("{n}", NameOf(first), StringComparison.Ordinal)
                .Replace("{p}", context.SourceDirectory, StringComparison.Ordinal)
                .Replace("{d}", context.DestinationDirectory, StringComparison.Ordinal));
        }

        return result;
    }

    private static string NameOf(string path) => Path.GetFileName(Path.TrimEndingDirectorySeparator(path));

    private static string Quoted(IEnumerable<string> values) => string.Join(' ', values.Select(v => $"\"{v}\""));

    public static IReadOnlyList<string> Tokenize(string text)
    {
        var tokens = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;
        var hasToken = false;

        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (c == '"')
            {
                if (inQuotes && i + 1 < text.Length && text[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }

                hasToken = true;
            }
            else if (char.IsWhiteSpace(c) && !inQuotes)
            {
                if (hasToken)
                    tokens.Add(current.ToString());
                current.Clear();
                hasToken = false;
            }
            else
            {
                current.Append(c);
                hasToken = true;
            }
        }

        if (hasToken)
            tokens.Add(current.ToString());
        return tokens;
    }
}
