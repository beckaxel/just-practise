# Technologiekonzept

> Arbeitsstand, noch nicht versioniert. Die fachlichen Anforderungen stehen in [funktionen.md](funktionen.md).

## Entscheidung

**just practise wird eine native Desktop-App in C#** mit:

- **Sprache:** C# auf .NET (aktuelle LTS-Version, derzeit .NET 10), Nullable Reference Types aktiv
- **UI-Framework:** [Avalonia](https://avaloniaui.net/) (ab Version 12.1)
- **Audio:** die plattformübergreifende .NET-Audio-Engine [SoundFlow](https://github.com/LSXPrime/SoundFlow) (gewählt in Prüfpunkt 2, [OwnAudioSharp](https://github.com/ModernMube/OwnAudioSharp) bleibt Ersatz)
- **Nativer Code:** Einzelne rechenintensive Bausteine (z. B. Time-Stretching) dürfen in C oder C++ umgesetzt und per P/Invoke eingebunden werden.
- **Plattformen:** Linux, Windows und macOS

Die Entscheidung steht unter dem Vorbehalt der [Prüfpunkte für den Prototyp](#prüfpunkte-für-den-prototyp).

### Begründung

- **Freier Dateizugriff:** Die App liest und schreibt direkt im Dateisystem. Es gibt keine Ordnerfreigaben, keine Berechtigungen, die verfallen, und keine gesperrten Ordner. Das passt zum Dateibrowser und zu den Sidecar-Dateien.
- **Unabhängig vom Browser:** Die App läuft überall gleich, ohne Einschränkung auf bestimmte Browser.
- **Audio ist machbar:** Es gibt inzwischen ausgereifte Audio-Engines für .NET mit MIT-Lizenz (Wiedergabe, Aufnahme, Mischen, Time-Stretching). Was fehlt, wird in C# oder bei Bedarf in C/C++ ergänzt. Das Metronom erzeugt seine Klicks direkt im Audio-Callback und ist damit sample-genau.
- **C# eignet sich für ein großes Projekt:** starke Typisierung, gute Werkzeuge, klare Struktur. C# ist die bevorzugte Sprache des Hauptentwicklers.
- **Avalonia** ist ausgereift, MIT-lizenziert und läuft auf allen drei Desktop-Betriebssystemen. Seit Version 12.1 unterstützt es Wayland nativ, einschließlich Touch. Gezeichnet wird über Skia, das reicht für Wellenform, Stimmgerät-Nadel und Notizen über den Noten.
- **Keine Laufzeitumgebung nötig:** Die App wird `self-contained` ausgeliefert, .NET muss nicht installiert sein.
- **Alle Tastenkürzel verfügbar:** Kein Browser beansprucht Kürzel für sich.

### Verworfene Alternativen

- **PWA (TypeScript, Svelte, Vite):** Die Web Audio API wäre sehr attraktiv gewesen. Dagegen sprechen die Beschränkung auf Chromium-Browser (Firefox und Safari unterstützen die File System Access API nicht), die nötige Ordnerfreigabe mit Berechtigungen, die der Browser verwaltet, und Tastenkürzel, die dem Browser gehören. Zudem installieren nur wenige Nutzer PWAs auf dem Desktop.
- **Electron:** schwergewichtig (rund 100 MB Installer, hoher Speicherbedarf) und umständlich in der Entwicklung (Haupt- und Renderer-Prozess, IPC).
- **Tauri:** leicht, nutzt aber die Web-Engine des Systems. Unter Linux ist das WebKitGTK; dort hängen Audioformate von GStreamer-Plugins ab, Mikrofonzugriff und AudioWorklet sind unsicher. Dazu kommt eine Rust-Toolchain.
- **Photino (C# mit eingebettetem WebView):** Das Projekt wird kaum noch gepflegt (letztes Release Januar 2025). Unter Linux setzt es ein installiertes WebKitGTK voraus.
- **Tauri mit C#-Kern als eigenem Prozess:** drei Toolchains, zwei Prozesse und dieselbe WebKitGTK-Abhängigkeit
- **.NET MAUI:** keine offizielle Unterstützung für Linux
- **Qt/QML (C++), Flutter (Dart):** höhere Hürde für Mitwirkende bzw. schwache Audio-Unterstützung auf dem Desktop

### Bewusste Einschränkungen

- **Kein Mobil:** Mobile Geräte sind kein Ziel. Avalonia könnte Android und iOS grundsätzlich bedienen, der Dateibrowser setzt aber freien Ordnerzugriff voraus.
- **Installation bzw. Paket pro Plattform:** Anders als eine Webseite muss die App heruntergeladen werden. Updates brauchen einen eingebauten Updater (siehe [Auslieferung](#auslieferung)).
- **Signierung:** Unsignierte Apps lösen unter macOS und Windows Warnungen aus. Die Signierung unter macOS kostet Geld (Apple Developer Program) und entfällt deshalb. Siehe [Signierung](#signierung).

## Anforderungen an die Technologie

### Aus dem Funktionskonzept

- **Plattformen:** Desktop unter Linux, Windows und macOS
- **Einfache Installation:** ein Installer bzw. Paket pro Plattform und eine portable Variante (AppImage, ZIP)
- **Keine weitere Laufzeitumgebung:** .NET wird mitgeliefert.
- **Offline:** Die App läuft komplett offline. Es gibt keine Abhängigkeit von Online-Diensten. Von sich aus nimmt sie keinen Kontakt nach außen auf, auch nicht für Updates.
- **Schneller Start**
- **Touch-Bedienung:** über die Pointer-Events von Avalonia, einheitlich für Maus, Touch und Stift

### Aus dem Open-Source-Ansatz

- **Keine Kosten:** Das Projekt nutzt nur kostenlose Werkzeuge und Dienste. Nichts darf laufende Kosten verursachen, es gibt keine Werbung und keine Spenden. Fällt ein kostenloser Dienst weg, muss das Projekt ohne ihn auskommen, notfalls mit Einschränkungen (z. B. unsignierte Pakete).
- Kostenlose Werkzeuge: Beitragen geht ohne kostenpflichtige Lizenzen oder IDEs (.NET SDK, VS Code, Rider für nicht-kommerzielle Nutzung, Visual Studio Community)
- Entwickeln ist unter Linux, Windows und macOS gleichermaßen möglich
- Einfacher Einstieg: klonen, `dotnet run`. Nativer Code wird vorgebaut bereitgestellt (z. B. über die CI), damit Mitwirkende ohne C/C++-Toolchain auskommen.
- Verbreitete Sprache bzw. verbreitetes Framework, damit möglichst viele beitragen können
- Abhängigkeiten müssen lizenzrechtlich mit MIT verträglich sein. GPL-Bibliotheken werden gemieden (z. B. Rubber Band). LGPL-Bibliotheken sind mit MIT verträglich, wenn sie als eigenständige, dynamisch geladene Bibliothek eingebunden werden, die der Nutzer austauschen kann (z. B. FFmpeg). Statisch gelinkt werden sie nie. Ihr Lizenztext wird mitgeliefert, und der Quellcode der verwendeten Version ist verlinkt. FFmpeg wird ohne GPL- und `nonfree`-Bestandteile gebaut bzw. verwendet.

### Aus dem Audioplayer

- Decodieren von MP3, WAV, FLAC, OGG und M4A/AAC
- Time-Stretching (50–150 %) ohne Tonhöhenänderung
- Berechnen und Rendern einer Wellenform, auch bei langen Stücken
- Dateisystemzugriff für den Dateibrowser und die Sidecar-Dateien

### Aus dem Stimmgerät

- Audio-Eingang mit wählbarem Eingabegerät
- Tonhöhenerkennung in Echtzeit und eine flüssig animierte Nadel
- Messen des Pegels für die Lautstärkeanzeige

### Aus künftigen Funktionen

- Notenanzeige: PDFs und Bilder rendern
- Overlay mit Notizen über den Noten: Zeichnen mit Finger, Stift und Maus, mit Druckstärke

## Bibliotheken

Die Lizenzen sind vor dem Einsatz am jeweiligen Paket zu prüfen.

| Baustein | Lösung | Lizenz |
|---|---|---|
| Audio-Engine (Ausgabe, Eingabe, Mischen) | [SoundFlow](https://github.com/LSXPrime/SoundFlow) (Backend: miniaudio) | MIT |
| Decodieren MP3, WAV, FLAC, OGG | über die Audio-Engine (miniaudio) | MIT-0 / Public Domain |
| Decodieren M4A/AAC | FFmpeg, dynamisch geladen | LGPL |
| Time-Stretching | [signalsmith-stretch](https://github.com/Signalsmith-Audio/signalsmith-stretch) (C++, nur Header) über einen eigenen C-Wrapper und P/Invoke | MIT |
| Time-Stretching, Rückfallebene | WSOLA aus SoundFlow, einfachere Qualität | MIT |
| Wellenform | eigene Berechnung der Spitzenwerte, Darstellung mit Avalonia/Skia | – |
| Metronom | eigene Klickerzeugung im Audio-Callback, keine Bibliothek | – |
| Tonhöhenerkennung | eigene Umsetzung des McLeod-Verfahrens (MPM) oder von YIN in C# | – |
| Pegelmessung | eigene Berechnung (RMS, Spitzenwert) | – |
| Ton- und Akkordlogik | eigene Komponente; als Grundlage zu prüfen: [DryWetMIDI](https://github.com/melanchall/drywetmidi) (Noten, Akkorde, Skalen) | MIT |
| Übersetzungen (Platzhalter, Mehrzahl) | [MessageFormat.NET](https://github.com/jeffijoe/messageformat.net), Laden der Sprachdateien selbst umgesetzt | MIT |
| MVVM | [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet) | MIT |
| Dependency Injection | [Microsoft.Extensions.DependencyInjection](https://github.com/dotnet/runtime) | MIT |
| Notenanzeige | PDFium über [PDFiumCore](https://github.com/Dtronix/PDFiumCore) | BSD-3 / Apache 2.0 |
| Zeichnen | Pointer-Events mit Druckstärke aus Avalonia; Glättung der Striche nach dem Verfahren von perfect-freehand (MIT), nach C# portiert | – |
| Installer und Updates | [Velopack](https://velopack.io/) | MIT |
| Tests | xUnit v3 auf der Microsoft Testing Platform | Apache 2.0 |

## Projektaufbau

```
just-practise/
├── JustPractise.slnx            Solution im neuen Format
├── global.json                  legt die SDK-Version fest
├── Directory.Build.props        gemeinsame Einstellungen aller Projekte
├── Directory.Packages.props     zentrale Paketversionen
├── .editorconfig                Codestil
├── src/
│   ├── JustPractise.Core/       Fachlogik ohne Abhängigkeiten
│   ├── JustPractise.Audio/      Audio-Schicht
│   ├── JustPractise.Storage/    Dateisystem und Persistenz
│   └── JustPractise.App/        Avalonia-Oberfläche
├── native/
│   └── stretch/                 C-Wrapper um signalsmith-stretch (CMake)
├── tests/
│   ├── JustPractise.Core.Tests/
│   ├── JustPractise.Audio.Tests/
│   └── JustPractise.Storage.Tests/
├── prototypes/                  kleine Testprogramme für einzelne Prüfpunkte
└── docs/
```

### Projekte

| Projekt | Inhalt | Darf verwenden |
|---|---|---|
| **Core** | Gruppierung zu Stücken, Modelle für Sidecar-Dateien und Einstellungen, Ton- und Akkordnamen, Logik von Timer und Metronom (z. B. Taktart, Betonung), Tastenbelegung | nichts |
| **Audio** | Schnittstelle zur Audio-Engine und deren Umsetzung, Player (Loop, Tempo, Lautstärke), Klickerzeugung des Metronoms, Tonhöhenerkennung, Pegelmessung, Spitzenwerte der Wellenform, Anbindung des nativen Codes | Core |
| **Storage** | Durchlaufen der Ordner, atomares Lesen und Schreiben der Sidecar-Dateien, Einstellungen und gerätespezifisches Gedächtnis, Caches, Dateiüberwachung | Core |
| **App** | Oberfläche nach MVVM, Übersetzungen, Zusammenstecken der Teile per Dependency Injection | alle |

- **Namensraum:** `JustPractise`
- **Austauschbare Audio-Engine:** Die Engine steckt hinter einer eigenen Schnittstelle. Der Rest der App kennt SoundFlow nicht, die Engine bleibt austauschbar.
- **Signalverarbeitung ohne Engine:** Tonhöhenerkennung, Pegelmessung und Klickerzeugung arbeiten nur auf Puffern. Getestet wird mit erzeugten Signalen (z. B. Sinus mit 440 Hz).
- **App nach Funktionen gegliedert:** `Shell/` (Hauptfenster), `FileBrowser/`, `Player/`, `Timer/`, `Metronome/`, `Tuner/`, `Settings/`, statt nach Art (`Views/`, `ViewModels/`). Alles zu einer Komponente liegt zusammen.
- **Gemeinsame Einstellungen** in `Directory.Build.props`: Nullable aktiv, Warnungen als Fehler, Analyzer aktiv. Paketversionen stehen zentral in `Directory.Packages.props`.

### Nativer Code

- Der Quellcode liegt unter `native/` und wird mit CMake gebaut.
- Die CI baut die Bibliotheken für alle Plattformen und hängt sie an ein GitHub-Release. Ein Skript lädt sie beim Build herunter, Mitwirkende brauchen keine C/C++-Toolchain. Wird das unhandlich, folgt später ein NuGet-Paket.
- Wer am nativen Code arbeitet, baut ihn lokal selbst. Der lokale Build hat Vorrang vor dem heruntergeladenen.

### Einstieg für Entwickler

- Der Einstieg soll unter Linux, Windows und macOS gleich einfach sein. Die Anleitung steht auf Englisch in `README.md` (Schnellstart) und `CONTRIBUTING.md` (Voraussetzungen pro System, Befehle, nativer Code), damit sie möglichst viele Mitwirkende erreicht. Die Konzeptdokumente bleiben auf Deutsch.
- Skripte für Entwickler liegen unter `tools/` und sind einzelne C#-Dateien, die mit `dotnet run` laufen. Einzige Voraussetzung ist das .NET SDK, das ohnehin nötig ist. Es gibt also ein Skript für alle Systeme statt Bash und PowerShell nebeneinander.
- `tools/setup.cs` prüft Git, CMake und einen C++-Compiler und nennt für fehlende Werkzeuge den Befehl des jeweiligen Systems (winget, apt, dnf, pacman, zypper, Homebrew bzw. `xcode-select`). Es installiert nie selbst etwas und ruft kein `sudo` auf. Danach baut es fehlende native Bibliotheken und die Solution.
- Sobald die CI fertige native Bibliotheken veröffentlicht, lädt `tools/setup.cs` sie herunter. Bis dahin braucht nur `prototypes/StretchCheck` eine native Bibliothek, die App baut auch ohne C++-Werkzeuge.

## Datenhaltung

### Dateizugriff

- Der Nutzer wählt einen oder mehrere Ordner über den Ordnerdialog von Avalonia (`StorageProvider`). Die App merkt sich die Pfade im gerätespezifischen Gedächtnis.
- Gelesen und geschrieben wird direkt mit `System.IO`. Es gibt keine Berechtigungsabfragen.
- **Nie vom UI-Thread:** Die Klassen in Storage blockieren, weil Dateien in Cloud-Ordnern erst geladen werden müssen (siehe Prüfpunkt 2). Die App ruft sie im Hintergrund auf.
- **Erst lesen, dann schreiben:** Eine Datendatei wird nur geschrieben, wenn sie beim letzten Lesen fehlte oder lesbar war. Eine Datei, die sich nicht lesen lässt (z. B. Cloud-Laufwerk nicht eingebunden), gilt wie eine beschädigte und wird nicht überschrieben.
- Änderungen im Dateisystem erkennt die App über `FileSystemWatcher`. Bei Netzlaufwerken und Cloud-Ordnern ist das unzuverlässig, dort ergänzt ein Abfragen des Änderungszeitpunkts.
- Unter Linux begrenzt inotify die Zahl der überwachten Ordner (`max_user_watches`). Bei großen Musiksammlungen werden nur geöffnete Ordner überwacht.

### Regeln für alle Dateien

Sie gelten für Sidecar-Dateien, Einstellungen und das gerätespezifische Gedächtnis.

- **JSON** über `System.Text.Json`, als UTF-8, eingerückt und damit für Menschen lesbar. Umlaute stehen lesbar in der Datei, nicht als `\u00FC`. Kommentare und Kommas am Ende werden beim Lesen toleriert. Ohne Quellgenerator: Er setzt fehlende `init`-Eigenschaften auf `null` statt auf ihren Standardwert und unterstützt `init` bei unbekannten Feldern nicht.
- **Modelle und Format in Core:** Die Records und die Regeln dieses Abschnitts liegen in `JustPractise.Core` (`AppDataFiles`), Storage liest und schreibt nur die Dateien.
- **Feldnamen** englisch in camelCase, unabhängig von der Sprache der Oberfläche
- **Versionsnummer:** Jede Datei beginnt mit `"format": 1`. Ändert sich das Format, migriert die App ältere Dateien beim Lesen schrittweise auf die aktuelle Version.
- **Nur Abweichungen speichern:** Fehlt ein Feld, gilt der Standardwert. Ändert sich ein Standard in einer neuen Version, wirkt er auch für bestehende Dateien.
- **Einheit im Feldnamen,** wo es eine gibt, z. B. `startMs`, `volumeDb`, `referenceHz`
- **Unbekannte Felder bleiben erhalten:** Liest eine ältere App-Version eine Datei, die eine neuere geschrieben hat, behält sie beim Speichern die Felder, die sie nicht kennt (`[JsonExtensionData]`). Das ist wichtig, wenn Ordner oder Einstellungen über die Cloud auf Rechnern mit verschiedenen Versionen liegen.
- **Datei aus der Zukunft:** Ist `format` höher, als die App kennt, liest sie die Datei, soweit sie kann, überschreibt sie aber nicht.
- **Beschädigte Datei:** Die App nutzt Standardwerte, zeigt einen Hinweis und überschreibt die Datei nicht.
- **Atomares Schreiben:** in eine temporäre Datei im selben Ordner schreiben und sie danach per `File.Move(..., overwrite: true)` bzw. `File.Replace` an die Stelle der alten setzen. Auf demselben Laufwerk ist das ein atomares Umbenennen.

### Sidecar-Dateien

- Pro Stück gibt es eine Sidecar-Datei, benannt nach dem erkannten Titel (z. B. `Autumn Leaves.jps`).
- Eine Erkennungslogik gruppiert Dateien zu Stücken. Sie ist ein reines C#-Modul ohne UI, mit Unit-Tests über viele Beispieldateinamen.

Beispiel:

```json
{
  "format": 1,
  "files": {
    "Autumn Leaves - Demo.mp3": {
      "loop": { "startMs": 12350, "endMs": 47800, "enabled": true },
      "tempo": 0.8,
      "volumeDb": -3.0
    },
    "Autumn Leaves - Playalong 80.mp3": {
      "volumeDb": 2.5
    }
  },
  "members": [
    "Autumn Leaves - Demo.mp3",
    "Autumn Leaves - Playalong 80.mp3",
    "Autumn Leaves.pdf"
  ]
}
```

- **`files`:** Einstellungen pro Audiodatei, über den Dateinamen ohne Pfad zugeordnet. Alle Dateien eines Stücks liegen im selben Ordner.
- **Loop:** Start und Ende in ganzen Millisekunden. Das ist genau genug für von Hand gesetzte Marker und hängt nicht von der Samplerate ab. Beim Abspielen rechnet die App auf Samples um. `enabled` hält fest, ob der Loop an ist.
- **Tempo:** als Faktor, `0.8` entspricht 80 %
- **Lautstärke:** Verstärkung in dB, 0 heißt unverändert
- **`members`:** nur vorhanden, wenn die Gruppierung von Hand korrigiert wurde. Die Liste hat Vorrang vor der automatischen Erkennung. Eine Datei, die hier steht, ordnet die Erkennung keinem anderen Stück mehr zu.
- **Umbenennen in der App:** über `File.Move`. Die App passt dabei die Einträge in `files` und `members` an.
- **Verwaiste Einträge:** Wird eine Datei außerhalb der App umbenannt oder gelöscht, bleibt ihr Eintrag erhalten. Taucht die Datei wieder auf, sind ihre Einstellungen noch da. Die App löscht verwaiste Einträge nicht.
- **Notizen zu Noten:** Handschriftliche Notizen können groß werden. Ob sie in die `.jps` oder in eine eigene Datei pro Notenblatt kommen, wird bei der Notenanzeige entschieden.

### Einstellungen und gerätespezifisches Gedächtnis

- **Gerätespezifisches Gedächtnis:** liegt als `device.json` im Anwendungsdatenordner des Systems (Linux `~/.config/just-practise`, Windows `%APPDATA%\just-practise`, macOS `~/Library/Application Support/just-practise`). Es bleibt immer lokal und enthält die Musikordner, den Speicherort der Einstellungen, den zuletzt geöffneten Ordner, das Eingabegerät und die Zustände der Komponenten.
- **Caches** (z. B. Wellenformen) liegen getrennt im Cache-Ordner des Systems (Linux `~/.cache/just-practise`, Windows `%LOCALAPPDATA%\just-practise`, macOS `~/Library/Caches/just-practise`). Sie dürfen jederzeit gelöscht werden.
- **Einstellungen:** als `settings.json`, ohne eigenen Speicherort ebenfalls im Anwendungsdatenordner. Mit eigenem Speicherort in einem vom Nutzer gewählten Ordner, z. B. in einem Cloud-Ordner.

Beispiel für `settings.json` (gekürzt):

```json
{
  "format": 1,
  "language": { "ui": "de", "noteNames": "german", "accidentals": "sharp" },
  "timer": { "minutes": 25 },
  "metronome": {
    "current": { "bpm": 92, "meter": "7/8", "subdivision": "none", "sound": "woodblock" },
    "presets": [
      { "name": "Etüde 7/8, 92 BPM", "settings": { "bpm": 92, "meter": "7/8" } }
    ]
  },
  "tuner": {
    "referenceHz": 440,
    "instruments": [ { "name": "Tenorsax", "transposeSemitones": -14 } ],
    "selectedInstrument": "Tenorsax"
  },
  "shortcuts": { "metronome.toggle": "N" }
}
```

- **Gliederung:** ein Abschnitt pro Bereich (Sprache, Komponenten, Tastenkürzel). Abschnitte, die ganz auf Standardwerten stehen, und leere Listen werden nicht geschrieben.
- **Lautstärke des Metronoms:** steht als `metronome.volumeDb` neben `current`, nicht in einer Einstellung. Eine benannte Einstellung ändert die Lautstärke also nicht.
- **Tastenkürzel:** Gespeichert werden nur die vom Nutzer geänderten Kürzel. Neue Standardkürzel aus Updates kommen so automatisch an.
- **Instrumente** werden über ihren Namen angesprochen. Der Name muss eindeutig sein.
- **Export und Import** betreffen nur die Einstellungen. Die Exportdatei ist eine Kopie von `settings.json` mit der Endung `.jpsettings`, ein eigenes Format gibt es nicht. Ein Import ersetzt die Einstellungen komplett und läuft durch dieselbe Migration wie das Lesen.
- **Änderungen von außen:** Liegt die Einstellungsdatei in einem Cloud-Ordner, können zwei Rechner gleichzeitig schreiben. Die App schreibt immer die ganze Datei, der letzte Schreibvorgang gewinnt. Ändert sich die Datei von außen, lädt die App sie neu. Sie erkennt das über `FileSystemWatcher` und den Änderungszeitpunkt.

## Auslieferung

Pakete und Updates laufen über [Velopack](https://velopack.io/). Die App wird `self-contained` gebaut, .NET muss nicht installiert sein.

### Pakete

| Plattform | Installation | Portable Variante |
|---|---|---|
| Windows | `Setup.exe`, installiert pro Benutzer ohne Administratorrechte | ZIP |
| macOS | `.pkg` mit dem App-Bundle | ZIP mit der `.app` |
| Linux | AppImage, ein Paket für alle Distributionen | Das AppImage ist selbst portabel. |

- **Architekturen:** x64 und arm64 auf allen drei Plattformen. arm64 deckt Apple Silicon, Windows auf ARM und z. B. einen Raspberry Pi ab. Der native Code (Time-Stretching, FFmpeg) wird ebenfalls für beide Architekturen bereitgestellt.
- **Portable Variante:** läuft ohne Installation. Die Daten liegen wie bei der installierten Variante im Anwendungsdatenordner des Systems, nicht neben der App.
- **Flatpak:** vorerst nicht. Die Sandbox widerspricht dem freien Dateizugriff (nötig wäre `--filesystem=home` oder `host`), und Flatpak übernimmt die Updates selbst. Kann später kommen, falls Bedarf besteht.

### Bauen und Veröffentlichen

- Ein Versions-Tag (z. B. `v0.3.0`) startet den Build in GitHub Actions, auf Runnern für Linux, Windows und macOS.
- `vpk pack` erzeugt pro Plattform und Architektur die Pakete. Die CI hängt sie an das GitHub-Release.
- Die arm64-Builds kosten nichts extra: Für öffentliche Repositories stellt GitHub arm64-Runner kostenlos bereit, und .NET baut für andere Architekturen ohne Mehraufwand.

### Updates

- **Nur auf Knopfdruck:** Die App sucht nie von sich aus nach Updates. Der Nutzer stößt die Prüfung in den Einstellungen an.
- **Quelle:** die GitHub-Releases des Projekts
- **Ablauf:** Gibt es ein Update, zeigt die App die neue Version an. Der Nutzer entscheidet, ob es heruntergeladen wird. Installiert wird beim Neustart der App, nie mitten im Üben.
- **Delta-Updates:** Velopack lädt nur die Änderungen gegenüber der installierten Version.
- **Ohne Netz:** Die App meldet, dass keine Verbindung besteht. Sonst passiert nichts.
- Ein automatisches Prüfen (abschaltbar) kann später dazukommen, falls Nutzer es wünschen.

## Signierung

Signiert wird nur, was kostenlos möglich ist (siehe [Keine Kosten](#aus-dem-open-source-ansatz)).

- **Windows:** Die ersten Versionen erscheinen unsigniert. SmartScreen warnt dann, man startet die App über „Weitere Informationen“ und „Trotzdem ausführen“. Sobald das Projekt die Bedingungen erfüllt, wird die kostenlose Signierung für Open-Source-Projekte bei der [SignPath Foundation](https://signpath.org/) beantragt. Als Herausgeber steht dann „SignPath Foundation“ im Zertifikat.
- **macOS:** nur ad-hoc signiert, wie es Apple Silicon mindestens verlangt. Velopack erledigt das. Gatekeeper blockiert die App beim ersten Start, man gibt sie unter „Systemeinstellungen > Datenschutz & Sicherheit > Dennoch öffnen“ frei. Die Download-Seite erklärt das kurz.
- **Linux:** keine Signatur nötig. Jedes Release enthält SHA-256-Prüfsummen.
- **Keine Notarisierung unter macOS:** Sie setzt das kostenpflichtige Apple Developer Program voraus und entfällt dauerhaft.

### Umsetzung

- Signiert wird nur im Release-Workflow der CI. Schlüssel und Zugangsdaten liegen als Secrets in GitHub.
- Lokale Builds und Builds von Mitwirkenden sind unsigniert und brauchen keinen Zugang zu Schlüsseln.
- **macOS und Mikrofon:** Das App-Bundle bekommt im `Info.plist` einen Text, wofür das Mikrofon genutzt wird (`NSMicrophoneUsageDescription`). Ob macOS die Mikrofonfreigabe einer nur ad-hoc signierten App nach Updates erneut abfragt, ist offen. Mangels Mac prüft das das Projekt nicht selbst, Rückmeldungen aus der Community sind willkommen.

## Übersetzungen

Weder Avalonia noch .NET bringen Übersetzungen im JSON-Format mit. Die App nutzt deshalb eine eigene schlanke Lösung. Platzhalter und Mehrzahl übernimmt [MessageFormat.NET](https://github.com/jeffijoe/messageformat.net) (MIT).

### Sprachdateien

- Eine Datei pro Sprache, benannt nach dem Sprachcode: `en.json`, `de.json`. Sie liegen in `JustPractise.App/Localization/` und sind in die App eingebettet.
- **Englisch ist die Referenz:** Neue Texte kommen immer zuerst in `en.json`. Die anderen Sprachen übersetzen von dort.
- **Gliederung nach Komponenten,** passend zu den Ordnern der App. Die Schlüssel beschreiben die Bedeutung, nicht den englischen Text, z. B. `player.play`.
- **Name der Sprache:** `language.name` enthält den Namen der Sprache in ihr selbst, z. B. „Deutsch“ oder „Français“. So zeigt ihn die Sprachauswahl an.
- **Platzhalter und Mehrzahl** im ICU-MessageFormat. Platzhalter haben Namen, damit Übersetzer die Wortstellung frei wählen können. Die Mehrzahl deckt auch Sprachen mit mehreren Mehrzahlformen ab.

Beispiel:

```json
{
  "language": { "name": "Deutsch" },
  "player": {
    "play": "Abspielen",
    "tempo": "Tempo {percent} %"
  },
  "timer": {
    "remaining": "{minutes, plural, one {noch # Minute} other {noch # Minuten}}"
  }
}
```

### Eigene Sprachdateien ohne Neubau

- Zusätzlich lädt die App Sprachdateien aus dem Ordner `translations` im Anwendungsdatenordner (z. B. `~/.config/just-practise/translations/fr.json`).
- Eine Datei dort hat Vorrang vor der eingebetteten Datei derselben Sprache. So kann man eine neue Sprache oder eine verbesserte Übersetzung sofort in der App ausprobieren, ohne die App zu bauen.
- Fertige Übersetzungen kommen über einen Beitrag ins Repository und werden dann eingebettet.

### Verhalten

- **Rückfall:** Fehlt ein Schlüssel in einer Sprache, zeigt die App den englischen Text, fehlt er auch dort, den Schlüssel selbst. Eine unvollständige Übersetzung ist damit nutzbar.
- **Umschalten ohne Neustart:** XAML bindet Texte über eine Markup-Erweiterung, z. B. `{l:Tr player.play}`, an einen Übersetzungsdienst. Wechselt die Sprache, aktualisieren sich alle Texte.
- **Zahlen- und Datumsformat:** Die Sprache der Oberfläche legt die `CultureInfo` fest, z. B. `92,5` im Deutschen.
- **Ton- und Akkordnamen** stehen nicht in den Sprachdateien. Sie entstehen in der eigenen Komponente in Core (siehe [Weitere Festlegungen](#weitere-festlegungen)).

### Absicherung

- Ein Unit-Test prüft alle eingebetteten Sprachdateien: gültiges JSON, gültige ICU-Syntax, keine Schlüssel, die es in `en.json` nicht gibt, und dieselben Platzhalter wie in `en.json`.
- Später möglich: Ein Source Generator erzeugt aus `en.json` Konstanten für die Schlüssel. Tippfehler in Schlüsseln werden dann zu Compilerfehlern.
- Später möglich: Übersetzen über [Weblate](https://weblate.org/), kostenlos für Open-Source-Projekte. Es versteht JSON mit ICU-Syntax, das Format passt also bereits.

## Weitere Festlegungen

- **Tastenkürzel** wirken global im Fenster, unabhängig vom Fokus, außer in Textfeldern. Die Belegung ist frei änderbar und wird aus den Einstellungen geladen, statt fest im Code zu stehen.
- **Mehrsprachigkeit von Anfang an:** Kein Text steht fest im Code, alle Texte kommen aus Übersetzungsdateien im JSON-Format. Sie sind für Mitwirkende ohne Programmierkenntnisse bearbeitbar. Details unter [Übersetzungen](#übersetzungen).
- **Ton- und Akkordnamen** erzeugt eine eigene, gut testbare Komponente, unabhängig von der Sprache der Oberfläche. Intern rechnet die App nur mit Tonhöhen und Akkordtypen, die Namen entstehen erst bei der Anzeige.
- **Struktur gegen Wildwuchs:** Fachlogik (Gruppierung, Ton- und Akkordnamen, Einstellungsformat) liegt in reinen Bibliotheken ohne UI und wird mit xUnit getestet. Audio ist eine eigene, abgekapselte Schicht. Die Oberfläche folgt MVVM: Views enthalten nur Darstellung und Bedienung.
- **Kleine, abgeschlossene Klassen:** Klassen sind standardmäßig `sealed`. Gemeinsames Verhalten entsteht durch Komposition aus kleinen Klassen, nicht durch Basisklassen. Vererbung ist die begründete Ausnahme. Die Vererbung, die das Framework vorgibt, bleibt flach (Views von `UserControl`, ViewModels von `ObservableObject`).
- **Interfaces nur an echten Grenzen,** z. B. Audio-Engine, Dateisystem, Speichern der Einstellungen. Nicht jede Klasse bekommt ein Interface.
- **Records für Daten,** z. B. Inhalt der Sidecar-Dateien, Einstellungen, Metronom-Einstellungen. Sie sind unveränderlich.
- **Dependency Injection:** Klassen bekommen ihre Abhängigkeiten über den Konstruktor. Der Container wird nur beim Start der App angefasst, dort steht, welche Klasse wofür verwendet wird. Keine Klasse holt sich selbst etwas aus dem Container, `IServiceProvider` wird nie injiziert. ViewModels werden ebenfalls über den Container erzeugt. Reine Fachlogik in Core wird einfach mit `new` erzeugt.
- **Zeit über `TimeProvider`:** Timer und Metronom fragen die Zeit über `TimeProvider` ab, damit Tests die Zeit gezielt vorlaufen lassen können.
- **Regeln für den Audio-Thread:** Im Audio-Callback wird kein Speicher angelegt und nicht gesperrt (keine `lock`s, kein I/O). Puffer werden vorab angelegt. Die Objekte im Audio-Callback werden einmal zusammengesteckt, im laufenden Betrieb erzeugt der Container nichts. Zwischen UI und Audio vermitteln lock-freie Warteschlangen. So stört die Speicherbereinigung von .NET die Wiedergabe möglichst nicht. Reicht das nicht, wandert das Mischen in nativen Code und C# steuert nur noch.
- **Regeln für die Speicherbereinigung** (aus Prüfpunkt 1): Die App nutzt den Workstation-GC mit Hintergrund-GC und legt das in der Projektdatei fest (`ServerGarbageCollection` aus, `ConcurrentGarbageCollection` an). Sie ruft nie `GC.Collect` auf und schaltet die Verdichtung des Large Object Heap nicht ein. Langlebige Daten wie decodiertes Audio, Wellenform-Spitzen oder Caches liegen in wenigen großen Arrays statt in vielen kleinen Objekten, und sehr viele langlebige Objekte werden nicht auf einmal angelegt. Vor dem Start der Wiedergabe rechnen Player und Metronom einige Blöcke ins Leere, damit JIT und erste Berechnungen nicht im Audio-Callback anfallen. Die App zählt verspätete Callbacks und zeigt sie in einer Diagnoseansicht, damit Aussetzer im echten Betrieb auffallen.
- **Nativer Code** hat eine schlanke C-Schnittstelle und wird per P/Invoke angesprochen. Die CI baut ihn für alle Plattformen.

## Prüfpunkte für den Prototyp

Der Prototyp entsteht unter Debian und wird danach unter Windows geprüft. Unter macOS prüft das Projekt mangels Gerät nicht selbst, das bleibt der Community überlassen. Er wird gleich in der endgültigen [Projektstruktur](#projektaufbau) gebaut, vor allem die Audio-Schicht bleibt erhalten. Prüfpunkte außerhalb davon (z. B. AAC, Touch) werden als kleine Testprogramme unter `prototypes/` geprüft.

1. **Aussetzer durch Speicherbereinigung:** Laufen Wiedergabe, Time-Stretching und Metronom gleichzeitig ohne Aussetzer, auch wenn die UI unter Last steht und Speicher anlegt? Welche Puffergrößen sind nötig, unter PipeWire/PulseAudio und Windows?

   **Ergebnis (Debian und Windows 11, November 2026): Der Audio-Callback bleibt vorerst in C#, mit festen Regeln (siehe [Weitere Festlegungen](#weitere-festlegungen)).** Geprüft mit `prototypes/GcCheck`: Ein Avalonia-Fenster zeichnet und legt Speicher an, während eine Datei mit signalsmith-stretch bei 50 % und das Metronom laufen. `--matrix` fährt automatisch sechs Lastphasen mit verschiedenen Puffergrößen und GC-Einstellungen. Gemessen wurden verspätete Callbacks, Blöcke über Budget, GC-Pausen und unter Linux die xruns von PipeWire. Gehörte Aussetzer wurden per Leertaste markiert.
   - **Normalbetrieb ohne Aussetzer:** Mit Workstation-GC und Hintergrund-GC lief die Wiedergabe auf beiden Systemen ohne hörbare Aussetzer, auch bei 300 MB/s neu angelegtem Speicher und 8 Mio. dauerhaft lebenden Objekten. Die kleinen GCs hielten höchstens 3–10 ms (Debian) bzw. 1,5–3 ms (Windows) an. Die vollen GCs liefen von selbst immer im Hintergrund.
   - **Blockierende volle GCs sind hörbar:** Der Audio-Callback läuft in verwaltetem Code, ein blockierender voller GC hält ihn an. Gemessen bei erzwungenem GC: 35–54 ms ohne zusätzliche Objekte (Heap 125 MB), 103–115 ms mit 1 Mio. Objekten, 540–810 ms mit 8 Mio. Objekten. Unter Debian war jeder davon hörbar, unter Windows bei kleinem Speicher nur ein Teil. Dasselbe passiert, wenn sehr viele langlebige Objekte auf einmal entstehen. Größere Puffer helfen dagegen nicht.
   - **Ausgeschieden:** Server-GC (kleine GCs bis 187 ms) und GC ohne Hintergrund-GC (260–330 ms Pause auch ohne erzwungenen GC). `GCLatencyMode.SustainedLowLatency` brachte keinen Vorteil.
   - **Puffergröße:** 10 ms reichen, 20 ms ebenso. 5 ms sind unter Debian zu knapp (einzelne verspätete Callbacks schon in Ruhe). Unter Windows kommen die Callbacks bei 5 ms paarweise, weil Windows intern in 10-ms-Schritten arbeitet.
   - **Vorrechnen vor dem Start:** senkt den längsten Block in der ersten Sekunde von 3 auf 0,7–1,3 ms. Die Spitzen von 5–10 ms aus Prüfpunkt 3 traten damit nicht mehr auf.
   - **Bluetooth:** Unter Debian erzeugte die Ausgabe über Bluetooth zusätzliche Aussetzer, die nicht vom Callback kamen. Gemessen wurde deshalb über die eingebauten Lautsprecher.
   - **Entscheidung:** Ein blockierender voller GC kommt im Normalbetrieb nicht vor, ist aber nicht ausgeschlossen (Speichermangel, starke Zersplitterung, `GC.Collect` in einer Bibliothek). Die App hält sich an die Regeln für die Speicherbereinigung und zählt verspätete Callbacks. Treten im echten Betrieb Aussetzer auf, wandert der ganze Audio-Callback (Stretching, Mischen, Klick, Loop) in nativen Code und C# steuert nur noch über eine lock-freie Warteschlange. Dafür liegt die Audio-Schicht hinter einer eigenen Schnittstelle, ein Wechsel betrifft nur sie. Ein nativer Ringpuffer mit 100–200 ms Vorlauf wurde verworfen, weil Tempowechsel, Springen und Metronom-Start dann verzögert reagieren.
2. **Audio-Engine:** SoundFlow oder OwnAudioSharp? Kriterien: Pflegezustand, API, Latenz, Ein- und Ausgabegeräte wählbar

   **Ergebnis (Debian, November 2026): SoundFlow 1.4.2.** Geprüft mit `prototypes/SoundFlowCheck`. OwnAudioSharp wurde nicht im Detail geprüft: Es zieht Avalonia in einer anderen Version, OnnxRuntime sowie VST- und MIDI-Unterstützung mit und bringt eine 16 MB große Bibliothek mit Rust-Backend. Es bleibt Ersatz, falls SoundFlow scheitert.
   - **API:** klar. Eine eigene Klangerzeugung (Metronom-Klick) ist eine einzige Klasse, die sample-genau im Audio-Callback läuft. Streaming, Springen, Loop-Punkte und WSOLA-Time-Stretching sind eingebaut. Eine 7-minütige MP3 ist in rund 50 ms geöffnet, weil gestreamt wird. Die Umrechnung von 44,1 auf 48 kHz stimmt.
   - **Latenz:** Rundlauf (Klick ausgeben, über Lautsprecher und Mikrofon wieder einlesen) im Median 20 ms bei 10 ms Puffer unter PipeWire. Bei 5, 10 und 20 ms Puffer kommen die Callbacks gleichmäßig, ohne Aussetzer und ohne hörbare Knackser, auch mit Wiedergabe, Metronom bis 400 bpm und Mikrofon gleichzeitig.
   - **Geräte:** Ein- und Ausgabegeräte lassen sich auflisten und wählen. Ein Wechsel des Ausgabegeräts oder der Puffergröße während der Wiedergabe funktioniert, ohne den Ablauf neu aufzubauen.
   - **Pflegezustand, Schwachpunkt:** Der Maintainer pausiert von Januar 2026 bis Februar 2027. Die Aufzählung `MiniAudioBackend` ist gegenüber miniaudio um eins verschoben (gemeldet wird „Oss“, aktiv ist PulseAudio). Mit der Standardwahl des Backends ist das harmlos. Das Risiko ist tragbar, weil SoundFlow unter MIT steht und sich notfalls forken lässt, die Geräteansteuerung im gut gepflegten miniaudio steckt und die Engine hinter einer eigenen Schnittstelle liegt.
   - **Windows 11 (November 2026, Laptop mit Realtek-Lautsprechern und Mikrofon-Array):** WASAPI läuft (gemeldet wird wegen der verschobenen Aufzählung „Null“). Klonen, `dotnet build` und `dotnet test` funktionieren ohne Anpassungen. Bei 10 und 20 ms Puffer kommen die Callbacks gleichmäßig, mit Wiedergabe, Metronom bis 400 bpm und Mikrofon gleichzeitig, ohne Aussetzer oder Knackser. Bei 5 ms kommen die Callbacks paarweise, weil Windows intern in 10-ms-Schritten arbeitet; hörbar ist das nicht. Folgt die Ausgabe dem Standardgerät, wechselt sie beim Verbinden eines Bluetooth-Lautsprechers automatisch mit, mit einer Lücke von rund 360 ms. Der Rundlauf liegt bei rund 100 ms (10 ms Puffer) bzw. 112 ms (20 ms Puffer), also mit etwa 90 ms festem Anteil, vermutlich durch die Signalverarbeitung des eingebauten Mikrofon-Arrays. Über Bluetooth sind es über 300 ms.
   - **Offen:** Rundlauf unter Windows mit einem externen Mikrofon oder ohne Audioverbesserungen gegenprüfen. Eine MP3 aus einem OneDrive-Ordner, die nur online lag, brauchte 5,4 s zum Öffnen. Das Öffnen darf die Oberfläche daher nie blockieren, und der Audio-Thread darf nie direkt auf eine Datei warten, die erst geladen wird (siehe Prüfpunkte 7 und 8).

3. **Time-Stretching:** Qualität und CPU-Last von signalsmith-stretch über P/Invoke im Vergleich zu WSOLA bei 50 % und 150 %. Lässt sich die native Bibliothek in der CI für alle Plattformen bauen?

   **Ergebnis (Debian und Windows 11, November 2026): signalsmith-stretch 1.4.0.** Geprüft mit `prototypes/StretchCheck`, einmal als Messung über 30 s eines Stücks (`render`, Blöcke wie bei 10 ms Puffer), einmal als Wiedergabe im echten Audio-Callback (`play`). Verglichen wurden signalsmith in der Standard-Einstellung, mit `split` und mit `cheaper` gegen das in SoundFlow eingebaute WSOLA (`Balanced` und `HighQuality`).
   - **Einbindung:** `native/stretch` ist ein kleiner C-Wrapper (`jp_stretch.h`) um signalsmith-stretch, gebaut mit CMake. signalsmith-stretch und signalsmith-linear 0.6.4 (beide MIT) holt CMake per `FetchContent`, festgelegt auf einen Commit. Die Bibliothek landet in `native/bin/<rid>/`. Unter Windows ist die C++-Laufzeit statisch eingebunden, unter Linux libstdc++ und libgcc, es braucht also kein VC-Redistributable und keine passende libstdc++.
   - **Klang:** signalsmith klingt bei 50 % und 150 % gut. `cheaper` ist kaum davon zu unterscheiden.
   - **CPU-Last:** signalsmith rechnet 40- bis 60-mal schneller als Echtzeit (Ø 0,2 ms pro 10-ms-Block), `cheaper` 60- bis 90-mal. WSOLA ist drei- bis sechsmal teurer, `HighQuality` braucht einzelne Blöcke von 11 bis 22 ms und reißt damit das Budget bei 10 ms Puffer auf beiden Systemen.
   - **Echtzeit:** In der Wiedergabe war unter Windows bei 10 und 20 ms Puffer kein Block über Budget, ohne hörbare Aussetzer. Unter Debian lag der längste Block unter 1,5 ms, unter Windows bei 5 bis 10 ms. Der Höchstwert fiel in die ersten Callbacks nach dem Start, vermutlich weil .NET den Code dort noch auf dem Audio-Thread übersetzt. Abhilfe (Vorrechnen vor dem Start, ReadyToRun) wird mit Prüfpunkt 1 und 9 geklärt.
   - **Latenz:** signalsmith verzögert um 120 ms (`split` 150 ms, `cheaper` 140 ms), WSOLA um 46 bis 93 ms. Wie sich das beim Starten, Springen und Loopen auswirkt, klärt der Einbau in den Player.
   - **WSOLA in SoundFlow:** verwirft Ausgabe, die nicht in den Puffer des Callbacks passt. Für den Einsatz wäre ein eigener Zwischenpuffer nötig. Es bleibt nur Ersatz, falls die native Bibliothek auf einer Plattform nicht läuft.
   - **Wahl:** signalsmith in der Standard-Einstellung. `cheaper` ist die Reserve für schwache Rechner, der Klangunterschied ist minimal.
   - **Offen:** Bau in der CI für alle Plattformen, einschließlich macOS und arm64 (zusammen mit Prüfpunkt 10).
4. **AAC:** Decodieren über dynamisch geladenes FFmpeg unter Linux, Windows und macOS, LGPL-konform. Unter Linux die System-Bibliotheken nutzen oder mitliefern? Wie groß wird das Paket?
5. **Mikrofon:** Wahl des Eingabegeräts, Latenz und stabile Tonhöhenerkennung
6. **Touch und Stift:** Avalonia unter Wayland und X11 sowie unter Windows, einschließlich Druckstärke
7. **Speicher bei langen Stücken:** ganz decodieren (10 Minuten Stereo ≈ 200 MB) oder in Teilen streamen?

   **Ergebnis (Debian und Windows 11, November 2026): ganz decodieren, als float, in Blöcken fester Größe (4 MB) aus einem Pool.** Geprüft mit `prototypes/DecodeCheck` (Konsole, ohne Tonausgabe) an den drei größten MP3s der Testsammlung (6,6–7,9 min). Ausgelegt ist die App auf Stücke bis 15 Minuten; das sind bei 48 kHz Stereo rund 350 MB als float.
   - **Decodieren ist schnell:** 560- bis 900-mal schneller als Echtzeit, eine 8-minütige MP3 ist in 0,5–0,8 s ganz decodiert. Die ersten 5 s liegen nach 20–40 ms vor, die Wiedergabe kann also starten, während der Rest im Hintergrund decodiert wird. Die Wellenform-Spitzen (je 256 Frames) aus dem decodierten Puffer brauchen rund 200 ms. Die Ergebnisse unter Debian und Windows unterscheiden sich kaum.
   - **Streamen springt nicht sample-genau:** Nach einem Sprung lag der Decoder je nach Datei und Ziel 0, −64 oder +47 Frames neben dem Ziel und meldete trotzdem die Zielposition, auf beiden Systemen gleich. Für Loops und Sprünge beim Üben ist ganz decodieren daher auch fachlich die bessere Wahl. Ein Sprung dauerte 0,3–6,7 ms.
   - **float statt short:** short halbiert den Speicher, decodiert aber nicht schneller und braucht beim Abspielen eine Umrechnung. Bei 15 Minuten als Obergrenze ist float tragbar.
   - **Blöcke aus einem Pool:** Ein neues großes Array pro Datei ließ beim schnellen Wechsel den Heap auf 2,1–2,8 GB wachsen und löste unter Windows einen blockierenden vollen GC von 72 ms aus. Mit dem Pool legt die nächste Datei nichts Neues an (2 MB statt 284 MB), der Inhalt ist identisch mit dem Array. Ein Block enthält nur ganze Frames, so liegt ein Frame nie über einer Blockgrenze.
   - **Großer Puffer und GC:** Ein Puffer von 350 MB verlängert die GC-Pausen unter Debian nicht (kleine GCs höchstens 3,3 ms). Unter Windows waren die kleinen GCs in diesem Lauf allgemein länger (bis 16 ms mit einem float-Array, 2,8 ms mit dem Pool), und im Szenario mit dem Pool hielt einmal ein Hintergrund-GC 48 ms an. Das wäre hörbar. Es kam nur einmal vor, unter Debian lag derselbe Fall bei höchstens 0,7 ms, ein Zusammenhang mit dem Pool ist daher unwahrscheinlich. Die Last (100 MB/s, 500 000 lebende Objekte, dazu Reste der vorherigen Szenarien) ist härter als eine echte Oberfläche.
   - **Tags nicht lesen:** SoundFlow erkennt Dateien mit ID3v2.2-Tags (gut die Hälfte der Testsammlung) nicht als MP3 und decodiert sie dann fünfmal langsamer, linear auf 48 kHz umgerechnet. Mit `ReadOptions.ReadTags = false` stimmt es. Ohne Tags schätzt SoundFlow die Länge dieser Dateien vorab doppelt so lang. Die App verlässt sich daher nicht auf die Schätzung, sondern nimmt die Länge nach dem Decodieren.
   - **Offen:** Der Pool gibt Speicher nie zurück; wann er schrumpft, klärt der Bau des Players. Der Hintergrund-GC von 48 ms unter Windows wird im Player mit dem Zähler für verspätete Callbacks beobachtet.
8. **Dateiüberwachung:** `FileSystemWatcher` bei großen Musikordnern und in Cloud-Ordnern
9. **Startzeit und Paketgröße:** mit und ohne Native AOT bzw. ReadyToRun
10. **arm64:** Bringen Audio-Engine und FFmpeg native Bibliotheken für arm64 unter allen drei Plattformen mit? Läuft das AppImage unter Debian ohne zusätzliche Pakete (FUSE)?

## Offene Punkte

Derzeit keine. Der nächste Schritt ist der Prototyp.
