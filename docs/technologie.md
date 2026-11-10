# Technologiekonzept

> Arbeitsstand, noch nicht versioniert. Die fachlichen Anforderungen stehen in [funktionen.md](funktionen.md).

## Entscheidung

**just practise wird eine native Desktop-App in C#** mit:

- **Sprache:** C# auf .NET (aktuelle LTS-Version, derzeit .NET 10), Nullable Reference Types aktiv
- **UI-Framework:** [Avalonia](https://avaloniaui.net/) (ab Version 12.1)
- **Audio:** eine plattformübergreifende .NET-Audio-Engine, [SoundFlow](https://github.com/LSXPrime/SoundFlow) oder [OwnAudioSharp](https://github.com/ModernMube/OwnAudioSharp); die Wahl trifft der Prototyp
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
- **Installation bzw. Paket pro Plattform:** Anders als eine Webseite muss die App heruntergeladen werden. Updates brauchen einen eingebauten Updater.
- **Signierung:** Unsignierte Apps lösen unter macOS und Windows Warnungen aus. Die Signierung unter macOS kostet Geld (Apple Developer Program).

## Anforderungen an die Technologie

### Aus dem Funktionskonzept

- **Plattformen:** Desktop unter Linux, Windows und macOS
- **Einfache Installation:** ein Installer bzw. Paket pro Plattform; eine portable Variante (z. B. AppImage, ZIP) wird angestrebt
- **Keine weitere Laufzeitumgebung:** .NET wird mitgeliefert.
- **Offline:** Die App läuft komplett offline. Es gibt keine Abhängigkeit von Online-Diensten.
- **Schneller Start**
- **Touch-Bedienung:** über die Pointer-Events von Avalonia, einheitlich für Maus, Touch und Stift

### Aus dem Open-Source-Ansatz

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
| Audio-Engine (Ausgabe, Eingabe, Mischen) | [SoundFlow](https://github.com/LSXPrime/SoundFlow) (Backend: miniaudio) oder [OwnAudioSharp](https://github.com/ModernMube/OwnAudioSharp) | MIT |
| Decodieren MP3, WAV, FLAC, OGG | über die Audio-Engine (miniaudio) | MIT-0 / Public Domain |
| Decodieren M4A/AAC | FFmpeg, dynamisch geladen | LGPL |
| Time-Stretching | [signalsmith-stretch](https://github.com/Signalsmith-Audio/signalsmith-stretch) (C++, nur Header) über einen eigenen C-Wrapper und P/Invoke | MIT |
| Time-Stretching, Rückfallebene | WSOLA aus SoundFlow, einfachere Qualität | MIT |
| Wellenform | eigene Berechnung der Spitzenwerte, Darstellung mit Avalonia/Skia | – |
| Metronom | eigene Klickerzeugung im Audio-Callback, keine Bibliothek | – |
| Tonhöhenerkennung | eigene Umsetzung des McLeod-Verfahrens (MPM) oder von YIN in C# | – |
| Pegelmessung | eigene Berechnung (RMS, Spitzenwert) | – |
| Ton- und Akkordlogik | eigene Komponente; als Grundlage zu prüfen: [DryWetMIDI](https://github.com/melanchall/drywetmidi) (Noten, Akkorde, Skalen) | MIT |
| MVVM | [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet) | MIT |
| Dependency Injection | [Microsoft.Extensions.DependencyInjection](https://github.com/dotnet/runtime) | MIT |
| Notenanzeige | PDFium über [PDFiumCore](https://github.com/Dtronix/PDFiumCore) | BSD-3 / Apache 2.0 |
| Zeichnen | Pointer-Events mit Druckstärke aus Avalonia; Glättung der Striche nach dem Verfahren von perfect-freehand (MIT), nach C# portiert | – |
| Installer und Updates | [Velopack](https://velopack.io/) | MIT |
| Tests | xUnit | Apache 2.0 |

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
- **Austauschbare Audio-Engine:** Die Engine steckt hinter einer eigenen Schnittstelle. Der Rest der App kennt weder SoundFlow noch OwnAudioSharp.
- **Signalverarbeitung ohne Engine:** Tonhöhenerkennung, Pegelmessung und Klickerzeugung arbeiten nur auf Puffern. Getestet wird mit erzeugten Signalen (z. B. Sinus mit 440 Hz).
- **App nach Funktionen gegliedert:** `FileBrowser/`, `Player/`, `Timer/`, `Metronome/`, `Tuner/`, `Settings/`, statt nach Art (`Views/`, `ViewModels/`). Alles zu einer Komponente liegt zusammen.
- **Gemeinsame Einstellungen** in `Directory.Build.props`: Nullable aktiv, Warnungen als Fehler, Analyzer aktiv. Paketversionen stehen zentral in `Directory.Packages.props`.

### Nativer Code

- Der Quellcode liegt unter `native/` und wird mit CMake gebaut.
- Die CI baut die Bibliotheken für alle Plattformen und hängt sie an ein GitHub-Release. Ein Skript lädt sie beim Build herunter, Mitwirkende brauchen keine C/C++-Toolchain. Wird das unhandlich, folgt später ein NuGet-Paket.
- Wer am nativen Code arbeitet, baut ihn lokal selbst. Der lokale Build hat Vorrang vor dem heruntergeladenen.

## Datenhaltung

### Dateizugriff

- Der Nutzer wählt einen oder mehrere Ordner über den Ordnerdialog von Avalonia (`StorageProvider`). Die App merkt sich die Pfade im gerätespezifischen Gedächtnis.
- Gelesen und geschrieben wird direkt mit `System.IO`. Es gibt keine Berechtigungsabfragen.
- Änderungen im Dateisystem erkennt die App über `FileSystemWatcher`. Bei Netzlaufwerken und Cloud-Ordnern ist das unzuverlässig, dort ergänzt ein Abfragen des Änderungszeitpunkts.
- Unter Linux begrenzt inotify die Zahl der überwachten Ordner (`max_user_watches`). Bei großen Musiksammlungen werden nur geöffnete Ordner überwacht.

### Regeln für alle Dateien

Sie gelten für Sidecar-Dateien, Einstellungen und das gerätespezifische Gedächtnis.

- **JSON** über `System.Text.Json`, als UTF-8, eingerückt und damit für Menschen lesbar
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

- **Gliederung:** ein Abschnitt pro Bereich (Sprache, Komponenten, Tastenkürzel)
- **Tastenkürzel:** Gespeichert werden nur die vom Nutzer geänderten Kürzel. Neue Standardkürzel aus Updates kommen so automatisch an.
- **Instrumente** werden über ihren Namen angesprochen. Der Name muss eindeutig sein.
- **Export und Import** betreffen nur die Einstellungen. Die Exportdatei ist eine Kopie von `settings.json` mit der Endung `.jpsettings`, ein eigenes Format gibt es nicht. Ein Import ersetzt die Einstellungen komplett und läuft durch dieselbe Migration wie das Lesen.
- **Änderungen von außen:** Liegt die Einstellungsdatei in einem Cloud-Ordner, können zwei Rechner gleichzeitig schreiben. Die App schreibt immer die ganze Datei, der letzte Schreibvorgang gewinnt. Ändert sich die Datei von außen, lädt die App sie neu. Sie erkennt das über `FileSystemWatcher` und den Änderungszeitpunkt.

## Weitere Festlegungen

- **Tastenkürzel** wirken global im Fenster, unabhängig vom Fokus, außer in Textfeldern. Die Belegung ist frei änderbar und wird aus den Einstellungen geladen, statt fest im Code zu stehen.
- **Mehrsprachigkeit von Anfang an:** Kein Text steht fest im Code, alle Texte kommen aus Übersetzungsdateien im JSON-Format. Sie sind für Mitwirkende ohne Programmierkenntnisse bearbeitbar.
- **Ton- und Akkordnamen** erzeugt eine eigene, gut testbare Komponente, unabhängig von der Sprache der Oberfläche. Intern rechnet die App nur mit Tonhöhen und Akkordtypen, die Namen entstehen erst bei der Anzeige.
- **Struktur gegen Wildwuchs:** Fachlogik (Gruppierung, Ton- und Akkordnamen, Einstellungsformat) liegt in reinen Bibliotheken ohne UI und wird mit xUnit getestet. Audio ist eine eigene, abgekapselte Schicht. Die Oberfläche folgt MVVM: Views enthalten nur Darstellung und Bedienung.
- **Kleine, abgeschlossene Klassen:** Klassen sind standardmäßig `sealed`. Gemeinsames Verhalten entsteht durch Komposition aus kleinen Klassen, nicht durch Basisklassen. Vererbung ist die begründete Ausnahme. Die Vererbung, die das Framework vorgibt, bleibt flach (Views von `UserControl`, ViewModels von `ObservableObject`).
- **Interfaces nur an echten Grenzen,** z. B. Audio-Engine, Dateisystem, Speichern der Einstellungen. Nicht jede Klasse bekommt ein Interface.
- **Records für Daten,** z. B. Inhalt der Sidecar-Dateien, Einstellungen, Metronom-Einstellungen. Sie sind unveränderlich.
- **Dependency Injection:** Klassen bekommen ihre Abhängigkeiten über den Konstruktor. Der Container wird nur beim Start der App angefasst, dort steht, welche Klasse wofür verwendet wird. Keine Klasse holt sich selbst etwas aus dem Container, `IServiceProvider` wird nie injiziert. ViewModels werden ebenfalls über den Container erzeugt. Reine Fachlogik in Core wird einfach mit `new` erzeugt.
- **Zeit über `TimeProvider`:** Timer und Metronom fragen die Zeit über `TimeProvider` ab, damit Tests die Zeit gezielt vorlaufen lassen können.
- **Regeln für den Audio-Thread:** Im Audio-Callback wird kein Speicher angelegt und nicht gesperrt (keine `lock`s, kein I/O). Puffer werden vorab angelegt. Die Objekte im Audio-Callback werden einmal zusammengesteckt, im laufenden Betrieb erzeugt der Container nichts. Zwischen UI und Audio vermitteln lock-freie Warteschlangen. So stört die Speicherbereinigung von .NET die Wiedergabe möglichst nicht. Reicht das nicht, wandert das Mischen in nativen Code und C# steuert nur noch.
- **Nativer Code** hat eine schlanke C-Schnittstelle und wird per P/Invoke angesprochen. Die CI baut ihn für alle Plattformen.

## Prüfpunkte für den Prototyp

Der Prototyp entsteht unter Debian und wird danach unter Windows geprüft. Er wird gleich in der endgültigen [Projektstruktur](#projektaufbau) gebaut, vor allem die Audio-Schicht bleibt erhalten. Prüfpunkte außerhalb davon (z. B. AAC, Touch) werden als kleine Testprogramme unter `prototypes/` geprüft.

1. **Aussetzer durch Speicherbereinigung:** Laufen Wiedergabe, Time-Stretching und Metronom gleichzeitig ohne Aussetzer, auch wenn die UI unter Last steht und Speicher anlegt? Welche Puffergrößen sind nötig, unter PipeWire/PulseAudio und Windows?
2. **Audio-Engine:** SoundFlow oder OwnAudioSharp? Kriterien: Pflegezustand, API, Latenz, Ein- und Ausgabegeräte wählbar
3. **Time-Stretching:** Qualität und CPU-Last von signalsmith-stretch über P/Invoke im Vergleich zu WSOLA bei 50 % und 150 %. Lässt sich die native Bibliothek in der CI für alle Plattformen bauen?
4. **AAC:** Decodieren über dynamisch geladenes FFmpeg unter Linux, Windows und macOS, LGPL-konform. Unter Linux die System-Bibliotheken nutzen oder mitliefern? Wie groß wird das Paket?
5. **Mikrofon:** Wahl des Eingabegeräts, Latenz und stabile Tonhöhenerkennung
6. **Touch und Stift:** Avalonia unter Wayland und X11 sowie unter Windows, einschließlich Druckstärke
7. **Speicher bei langen Stücken:** ganz decodieren (10 Minuten Stereo ≈ 200 MB) oder in Teilen streamen?
8. **Dateiüberwachung:** `FileSystemWatcher` bei großen Musikordnern und in Cloud-Ordnern
9. **Startzeit und Paketgröße:** mit und ohne Native AOT bzw. ReadyToRun

## Offene Punkte

- Bibliothek bzw. Mechanismus für Übersetzungen (JSON statt `.resx`)
- Auslieferung: Pakete pro Plattform (z. B. AppImage oder Flatpak, Installer für Windows, DMG), Updater, portable Variante
- Signierung unter Windows und macOS
