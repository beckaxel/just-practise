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
| Notenanzeige | PDFium über [PDFiumCore](https://github.com/Dtronix/PDFiumCore) | BSD-3 / Apache 2.0 |
| Zeichnen | Pointer-Events mit Druckstärke aus Avalonia; Glättung der Striche nach dem Verfahren von perfect-freehand (MIT), nach C# portiert | – |
| Installer und Updates | [Velopack](https://velopack.io/) | MIT |
| Tests | xUnit | Apache 2.0 |

## Datenhaltung

### Dateizugriff

- Der Nutzer wählt einen oder mehrere Ordner über den Ordnerdialog von Avalonia (`StorageProvider`). Die App merkt sich die Pfade im gerätespezifischen Gedächtnis.
- Gelesen und geschrieben wird direkt mit `System.IO`. Es gibt keine Berechtigungsabfragen.
- Änderungen im Dateisystem erkennt die App über `FileSystemWatcher`. Bei Netzlaufwerken und Cloud-Ordnern ist das unzuverlässig, dort ergänzt ein Abfragen des Änderungszeitpunkts.
- Unter Linux begrenzt inotify die Zahl der überwachten Ordner (`max_user_watches`). Bei großen Musiksammlungen werden nur geöffnete Ordner überwacht.

### Sidecar-Dateien

- Pro Stück gibt es eine Sidecar-Datei, benannt nach dem erkannten Titel (z. B. `Autumn Leaves.jps`). Der Inhalt ist JSON (`System.Text.Json`).
- **Atomares Schreiben:** in eine temporäre Datei im selben Ordner schreiben und sie danach per `File.Move(..., overwrite: true)` bzw. `File.Replace` an die Stelle der alten setzen. Auf demselben Laufwerk ist das ein atomares Umbenennen.
- **Umbenennen:** über `File.Move`
- Eine Erkennungslogik gruppiert Dateien zu Stücken. Sie ist ein reines C#-Modul ohne UI, mit Unit-Tests über viele Beispieldateinamen.

### Einstellungen und gerätespezifisches Gedächtnis

- **Gerätespezifisches Gedächtnis:** liegt als JSON-Datei im Anwendungsdatenordner des Systems (Linux `~/.config/just-practise`, Windows `%APPDATA%\just-practise`, macOS `~/Library/Application Support/just-practise`). Es bleibt immer lokal und enthält die Musikordner, den Speicherort der Einstellungen, den zuletzt geöffneten Ordner, das Eingabegerät und die Zustände der Komponenten.
- **Caches** (z. B. Wellenformen) liegen getrennt im Cache-Ordner des Systems (Linux `~/.cache/just-practise`, Windows `%LOCALAPPDATA%\just-practise`, macOS `~/Library/Caches/just-practise`). Sie dürfen jederzeit gelöscht werden.
- **Einstellungen:** ohne eigenen Speicherort ebenfalls als JSON-Datei im Anwendungsdatenordner. Mit eigenem Speicherort in einem vom Nutzer gewählten Ordner, z. B. in einem Cloud-Ordner.
- Export und Import betreffen nur die Einstellungen. Ein Import ersetzt sie komplett. Das Format braucht deshalb eine Versionsnummer, damit ältere Exporte später noch gelesen werden können.
- **Änderungen von außen:** Liegt die Einstellungsdatei in einem Cloud-Ordner, können zwei Rechner gleichzeitig schreiben. Die App schreibt atomar und erkennt Änderungen über `FileSystemWatcher` und den Änderungszeitpunkt.

## Weitere Festlegungen

- **Tastenkürzel** wirken global im Fenster, unabhängig vom Fokus, außer in Textfeldern. Die Belegung ist frei änderbar und wird aus den Einstellungen geladen, statt fest im Code zu stehen.
- **Mehrsprachigkeit von Anfang an:** Kein Text steht fest im Code, alle Texte kommen aus Übersetzungsdateien im JSON-Format. Sie sind für Mitwirkende ohne Programmierkenntnisse bearbeitbar.
- **Ton- und Akkordnamen** erzeugt eine eigene, gut testbare Komponente, unabhängig von der Sprache der Oberfläche. Intern rechnet die App nur mit Tonhöhen und Akkordtypen, die Namen entstehen erst bei der Anzeige.
- **Struktur gegen Wildwuchs:** Fachlogik (Gruppierung, Ton- und Akkordnamen, Einstellungsformat) liegt in reinen Bibliotheken ohne UI und wird mit xUnit getestet. Audio ist eine eigene, abgekapselte Schicht. Die Oberfläche folgt MVVM: Views enthalten nur Darstellung und Bedienung.
- **Regeln für den Audio-Thread:** Im Audio-Callback wird kein Speicher angelegt und nicht gesperrt (keine `lock`s, kein I/O). Puffer werden vorab angelegt. Zwischen UI und Audio vermitteln lock-freie Warteschlangen. So stört die Speicherbereinigung von .NET die Wiedergabe möglichst nicht. Reicht das nicht, wandert das Mischen in nativen Code und C# steuert nur noch.
- **Nativer Code** hat eine schlanke C-Schnittstelle und wird per P/Invoke angesprochen. Die CI baut ihn für alle Plattformen.

## Prüfpunkte für den Prototyp

Der Prototyp entsteht unter Debian und wird danach unter Windows geprüft.

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

- Aufbau des Projekts (Solution, Projekte, Module)
- Format der Sidecar-Dateien und der Einstellungen im Detail
- Bibliothek bzw. Mechanismus für Übersetzungen (JSON statt `.resx`)
- Auslieferung: Pakete pro Plattform (z. B. AppImage oder Flatpak, Installer für Windows, DMG), Updater, portable Variante
- Signierung unter Windows und macOS
