# Dateimanager

Ein plattformübergreifender Dateimanager für Windows, Linux und macOS, gebaut mit
[Avalonia UI](https://avaloniaui.net/) 12 und .NET 10. Zwei Fenster nebeneinander wie im
**Total Commander**, darunter eine frei belegbare, farbige Knopfleiste wie bei **Directory Opus**
auf dem Amiga.

![Screenshot](docs/screenshot.png)

## Funktionen

- **Zwei Dateifenster:** jedes mit eigener Ortsauswahl (Benutzerordner, Bilder, Laufwerke …),
  Zurück/Vorwärts-Verlauf, Adressleiste (auch `~`), Filter und Statuszeile mit freiem Speicherplatz.
  Das aktive Fenster ist blau umrandet; **Tab** wechselt.
- **Total-Commander-Bedienung:** F5 kopiert und F6 verschiebt die Auswahl ins andere Fenster
  (Ziel lässt sich im Dialog ändern), F7 neuer Ordner, F8/Entf in den Papierkorb, F2/Umschalt+F6 umbenennen.
- **Knopfleiste:** Knöpfe mit eigener Beschriftung und eigenen Farben, die entweder einen eingebauten
  Befehl oder ein externes Programm ausführen. Rechtsklick auf einen Knopf zum Bearbeiten, Duplizieren,
  Verschieben oder Entfernen; Spaltenzahl im Menü *Knöpfe*.
- **Papierkorb:** Windows-Papierkorb, freedesktop.org-Papierkorb unter Linux, `~/.Trash` unter macOS.
- Sortierung per Spaltenkopf (Ordner bleiben oben), Mehrfachauswahl, versteckte Dateien,
  automatische Aktualisierung, Kontextmenü.
- Knöpfe, zuletzt geöffnete Ordner und Einstellungen werden gespeichert.

### Knöpfe konfigurieren

![Knopf-Editor](docs/knopf-editor.png)

Externe Programme starten im Ordner des aktiven Fensters. In den Argumenten stehen Platzhalter zur Verfügung:

| Platzhalter | Bedeutung |
| --- | --- |
| `{f}` | alle ausgewählten Dateien (als eigenes Argument: ein Argument je Datei) |
| `{s}` | erste ausgewählte Datei |
| `{n}` | Name der ersten ausgewählten Datei (ohne Pfad) |
| `{p}` | Ordner des aktiven Fensters (Quelle) |
| `{d}` | Ordner des anderen Fensters (Ziel) |

Argumente mit Leerzeichen in doppelte Anführungszeichen setzen. Mit *Für jede ausgewählte Datei
einzeln starten* wird das Programm pro Datei einmal aufgerufen. Beispiele:

| Knopf | Programm | Argumente |
| --- | --- | --- |
| ZIP packen | `zip` | `-r "{d}/archiv.zip" {f}` |
| Bild bearbeiten | `gimp` | `{f}` |
| Vergleichen | `meld` | `{s} "{d}/{n}"` |
| Shell-Befehl | `sh` | `-c "du -sh {f} \| sort -h"` |

Die Einstellungen liegen als JSON-Datei unter `%APPDATA%\Dateimanager\settings.json` (Windows)
bzw. `~/.config/Dateimanager/settings.json` (Linux/macOS) und können auch von Hand bearbeitet werden.

### Tastenkürzel

| Taste | Aktion |
| --- | --- |
| Tab | Anderes Fenster aktivieren |
| Enter / Doppelklick | Öffnen |
| Rücktaste, Alt+Hoch | Übergeordneter Ordner |
| Alt+Links / Alt+Rechts | Zurück / Vorwärts |
| F2, Umschalt+F6 | Umbenennen |
| F5 / F6 | Kopieren / Verschieben ins andere Fenster |
| F7 | Neuer Ordner |
| F8, Entf / Umschalt+Entf | In den Papierkorb / endgültig löschen |
| Strg+A | Alles auswählen |
| Strg+C / Strg+X / Strg+V | Kopieren / Ausschneiden / Einfügen (innerhalb der App) |
| Strg+U | Fenster tauschen |
| Strg+R | Beide Fenster aktualisieren |
| Strg+H | Versteckte Dateien ein-/ausblenden |
| Strg+L / Strg+F | Adressleiste / Filter des aktiven Fensters |

## Bauen und starten

Voraussetzung: [.NET 10 SDK](https://dotnet.microsoft.com/download).

```bash
dotnet run --project src/Dateimanager                       # zuletzt geöffnete Ordner
dotnet run --project src/Dateimanager -- /links /rechts     # bestimmte Ordner öffnen
dotnet test                                                 # Unit- und Headless-UI-Tests
```

Eigenständige Anwendung veröffentlichen, z. B. für Windows:

```bash
dotnet publish src/Dateimanager -c Release -r win-x64 --self-contained
```

## Aufbau

```
src/Dateimanager.Core/   Logik ohne UI-Abhängigkeit
  FileSystemService.cs     Auflisten, Anlegen, Umbenennen, Kopieren, Verschieben, Löschen
  TrashService.cs          Papierkorb je Betriebssystem
  ButtonDefinition.cs      Knöpfe, eingebaute Befehle, Standardbelegung
  CommandTemplate.cs       Platzhalter in Argumenten auflösen
  AppSettings.cs           Einstellungen als JSON laden/speichern
src/Dateimanager/        Avalonia-Oberfläche (MVVM mit CommunityToolkit.Mvvm)
  ViewModels/              MainWindow (zwei Fenster + Befehle), Pane (ein Dateifenster),
                           ButtonBank (Knopfleiste), ButtonEditor
  Views/                   MainWindow, PaneView, ButtonEditorWindow
  Services/                Dialoge, Öffnen/Starten von Programmen
tests/Dateimanager.Tests/ xUnit-Tests inkl. Avalonia.Headless für ViewModels und Fenster
```

## Ideen für die nächsten Schritte

- Mehrere Knopfbänke zum Umschalten (wie in Directory Opus)
- Interne Befehle für Archive (ZIP packen/entpacken) und Dateivergleich
- Drag & Drop zwischen den Fenstern und mit dem System
- Fortschrittsanzeige und Abbrechen bei großen Kopiervorgängen
- Tabs je Fenster, Dateibetrachter (F3)
