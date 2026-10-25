# Dateimanager

Ein plattformübergreifender Dateimanager für Windows, Linux und macOS, gebaut mit
[Avalonia UI](https://avaloniaui.net/) 12 und .NET 10.

![Screenshot](docs/screenshot.png)

## Funktionen

- **Navigation:** Zurück/Vorwärts-Verlauf, übergeordneter Ordner, editierbare Adressleiste (auch `~`),
  Seitenleiste mit persönlichen Ordnern und Laufwerken
- **Dateiliste:** Name, Größe, Typ, Änderungsdatum; Sortierung per Spaltenkopf (Ordner bleiben oben),
  Mehrfachauswahl, Filter nach Namen, versteckte Dateien ein-/ausblenden
- **Dateioperationen:** Öffnen mit dem Standardprogramm, neuer Ordner, Umbenennen,
  Kopieren/Ausschneiden/Einfügen (Namenskonflikte werden als „Name (2)“ gelöst),
  Löschen in den Papierkorb oder endgültig
- **Papierkorb:** Windows-Papierkorb, freedesktop.org-Papierkorb unter Linux
  (Wiederherstellen im Dateimanager des Desktops möglich), `~/.Trash` unter macOS
- **Automatische Aktualisierung**, wenn sich der angezeigte Ordner ändert
- Kontextmenü und Tastenkürzel

### Tastenkürzel

| Taste | Aktion |
| --- | --- |
| Enter / Doppelklick | Öffnen |
| Rücktaste, Alt+Hoch | Übergeordneter Ordner |
| Alt+Links / Alt+Rechts | Zurück / Vorwärts |
| F2 | Umbenennen |
| Entf / Umschalt+Entf | In den Papierkorb / endgültig löschen |
| Strg+C / Strg+X / Strg+V | Kopieren / Ausschneiden / Einfügen |
| Strg+Umschalt+N | Neuer Ordner |
| Strg+H | Versteckte Dateien ein-/ausblenden |
| Strg+L / Strg+F | Adressleiste / Filter fokussieren |
| F5 | Aktualisieren |

## Bauen und starten

Voraussetzung: [.NET 10 SDK](https://dotnet.microsoft.com/download).

```bash
dotnet run --project src/Dateimanager            # startet im persönlichen Ordner
dotnet run --project src/Dateimanager -- /pfad   # startet in einem bestimmten Ordner
dotnet test                                      # Unit- und Headless-UI-Tests
```

Eigenständige Anwendung veröffentlichen, z. B. für Windows:

```bash
dotnet publish src/Dateimanager -c Release -r win-x64 --self-contained
```

## Aufbau

```
src/Dateimanager.Core/   Dateisystem-Logik ohne UI-Abhängigkeit
  FileSystemService.cs     Auflisten, Anlegen, Umbenennen, Kopieren, Verschieben, Löschen
  TrashService.cs          Papierkorb je Betriebssystem
  NavigationHistory.cs     Zurück/Vorwärts-Verlauf
  Location.cs              Schnellzugriffe und Laufwerke
src/Dateimanager/        Avalonia-Oberfläche (MVVM mit CommunityToolkit.Mvvm)
  ViewModels/              MainWindowViewModel, FileEntryViewModel, LocationViewModel
  Views/MainWindow.axaml   Hauptfenster
  Services/                Dialoge und Öffnen mit dem Standardprogramm
tests/Dateimanager.Tests/ xUnit-Tests inkl. Avalonia.Headless für ViewModel und Fenster
```

## Ideen für die nächsten Schritte

- Tabs und geteilte Ansicht (zwei Ordner nebeneinander)
- Drag & Drop und Austausch über die System-Zwischenablage
- Symbol-/Kachelansicht mit Vorschaubildern
- Fortschrittsanzeige und Abbrechen bei großen Kopiervorgängen
- Rekursive Suche, Eigenschaften-Dialog, Favoriten
