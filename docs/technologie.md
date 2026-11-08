# Technologiekonzept

> Arbeitsstand, noch nicht versioniert. Die fachlichen Anforderungen stehen in [funktionen.md](funktionen.md).

## Entscheidung

**just practise wird eine Progressive Web App (PWA)** mit:

- **Sprache:** TypeScript im `strict`-Modus
- **UI-Framework:** Svelte
- **Build:** Vite
- **Laufzeit:** ein Chromium-Browser auf dem Desktop (Chrome, Edge, Brave, Vivaldi …) unter Linux, Windows und macOS

Die Entscheidung steht unter dem Vorbehalt der [Prüfpunkte für den Prototyp](#prüfpunkte-für-den-prototyp).

### Begründung

- **Audio ist der Kern der App.** Die Web Audio API bringt Wiedergabe, Mischen, Abspielgeschwindigkeit, Mikrofoneingang und eine sample-genaue Audio-Uhr mit. Eigene Verarbeitung (Time-Stretching, Tonhöhenerkennung) läuft in einem AudioWorklet in einem eigenen Echtzeit-Thread. Was im UI-Thread passiert, auch die Speicherbereinigung, stört die Wiedergabe nicht.
- **Fertige Bausteine:** Für Time-Stretching, Wellenform, Tonhöhenerkennung, Notenanzeige und Zeichnen gibt es ausgereifte Bibliotheken mit MIT-verträglichen Lizenzen (siehe [Bibliotheken](#bibliotheken)).
- **Touch, Stift, Zeichnen und PDF** sind im Browser am besten unterstützt.
- **Eine Codebasis für alle Desktop-Betriebssysteme.** Plattform-Builds gibt es nicht, macOS ist ohne Mehraufwand dabei.
- **Dateizugriff:** Die File System Access API erlaubt Lesen und Schreiben in vom Nutzer gewählten Ordnern samt Unterordnern. Das reicht für Dateibrowser und Sidecar-Dateien.
- **Einfacher Einstieg für Mitwirkende:** klonen, `npm install`, `npm run dev`, auf jedem System
- **Auslieferung und Updates:** Die App wird als statische Webseite veröffentlicht (z. B. GitHub Pages). Updates kommen automatisch.

### Verworfene Alternativen

- **Avalonia (C#):** Audio müsste über miniaudio und native Wrapper selbst angebunden werden, Touch unter Linux ist unsicher, Zeichnen und PDF wären Eigenbau.
- **Photino (C# mit eingebettetem WebView, auch mit Blazor oder Svelte):** Das Projekt wird kaum noch gepflegt (letztes Release Januar 2025). Unter Linux setzt es ein installiertes WebKitGTK voraus.
- **Tauri mit C#-Kern als eigenem Prozess:** umgeht Photino, bringt aber drei Toolchains, zwei Prozesse und dieselbe WebKitGTK-Abhängigkeit.
- **Electron:** zu groß, zu langsamer Start.
- **Qt/QML (C++), Flutter (Dart):** höhere Hürde für Mitwirkende bzw. schwache Audio-Unterstützung auf dem Desktop.

### Bewusste Einschränkungen

- **Nur Chromium-Browser:** Firefox und Safari unterstützen die File System Access API nicht. Ohne sie funktioniert der Dateibrowser nicht.
- **Kein Mobil:** Unter iOS/iPadOS gibt es die API nicht, unter Android ist die Ordnerauswahl nicht verlässlich verfügbar. Ein Ersatzmodus mit Import in einen privaten App-Speicher ist nicht praktikabel und wird nicht gebaut. Unterstützen mobile Browser die Ordnerauswahl später, läuft die App dort ohne Umbau.
- **Tastenkürzel:** Einige Kürzel gehören dem Browser und lassen sich nicht überschreiben (z. B. `Strg+W`). In der installierten PWA (eigenes Fenster) sind es weniger.

## Anforderungen an die Technologie

### Aus dem Funktionskonzept

- **Plattformen:** Desktop unter Linux, Windows und macOS, jeweils mit einem Chromium-Browser
- **Ohne Installation:** Die App läuft direkt im Browser. Optional lässt sie sich als PWA installieren und läuft dann in einem eigenen Fenster.
- **Keine weitere Laufzeitumgebung:** Außer einem Chromium-Browser ist nichts vorausgesetzt.
- **Offline:** Nach dem ersten Aufruf läuft die App über einen Service Worker komplett offline. Es gibt keine Abhängigkeit von Online-Diensten.
- **Schneller Start**
- **Touch-Bedienung:** über Pointer-Events, einheitlich für Maus, Touch und Stift

### Aus dem Open-Source-Ansatz

- Kostenlose Werkzeuge: Beitragen geht ohne kostenpflichtige Lizenzen oder IDEs
- Entwickeln ist unter Linux, Windows und macOS gleichermaßen möglich
- Einfacher Einstieg: klonen, bauen, starten in wenigen Schritten
- Verbreitete Sprache bzw. verbreitetes Framework, damit möglichst viele beitragen können
- Abhängigkeiten müssen lizenzrechtlich mit MIT verträglich sein. LGPL- und GPL-Bibliotheken werden gemieden (z. B. SoundTouchJS, peaks.js, Rubber Band).

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
- Overlay mit Notizen über den Noten: Zeichnen mit Finger, Stift und Maus

## Bibliotheken

Die Lizenzen sind vor dem Einsatz am jeweiligen Paket zu prüfen.

| Baustein | Lösung | Lizenz |
|---|---|---|
| Decodieren | `decodeAudioData`, im Browser eingebaut | – |
| Time-Stretching | [signalsmith-stretch](https://www.npmjs.com/package/signalsmith-stretch) (WASM, AudioWorklet) | MIT |
| Time-Stretching, Rückfallebene | `playbackRate` mit `preservesPitch`, im Browser eingebaut, einfachere Qualität | – |
| Wellenform | [wavesurfer.js](https://wavesurfer.xyz/) (Zoom, Zeitleiste, Regionen für A-B-Loops, vorberechnete Peaks) | BSD-3 |
| Metronom | eigene Planung auf der Audio-Uhr, keine Bibliothek | – |
| Tonhöhenerkennung | [pitchy](https://www.npmjs.com/package/pitchy) (McLeod-Verfahren) | MIT |
| Pegelmessung | eigene Berechnung (RMS, Spitzenwert) im AudioWorklet oder über `AnalyserNode` | – |
| Ton- und Akkordlogik | [tonal](https://github.com/tonaljs/tonal); die Anzeigenamen (H/B, festes Do) erzeugt eine eigene Komponente | MIT |
| Notenanzeige | [pdf.js](https://mozilla.github.io/pdf.js/) | Apache 2.0 |
| Zeichnen | [perfect-freehand](https://github.com/steveruizok/perfect-freehand) (druckabhängige Striche) | MIT |
| Tests | Vitest | MIT |

## Datenhaltung

### Dateizugriff

- Der Nutzer wählt einen oder mehrere Ordner (`showDirectoryPicker()`, Modus `readwrite`). Jeder Ordner liefert ein Handle, das für den ganzen Baum darunter gilt.
- Die Handles werden in IndexedDB gespeichert, damit die App die Ordner beim nächsten Start kennt.
- Beim Start prüft die App die Berechtigung (`queryPermission()`). Seit Chrome 122 kann der Nutzer den Zugriff dauerhaft erlauben. Sonst genügt ein Klick zum erneuten Freigeben.
- Systemordner sperrt der Browser, z. B. „Programme“, „AppData“ und vermutlich das Home-Verzeichnis selbst.
- Das Ergebnis des Ordnerdurchlaufs wird zwischengespeichert, weil das Durchlaufen im Browser langsamer ist als nativ.

### Sidecar-Dateien

- Pro Stück gibt es eine Sidecar-Datei, benannt nach dem erkannten Titel (z. B. `Autumn Leaves.jps`). Der Inhalt ist JSON.
- **Atomares Schreiben:** `createWritable()` schreibt in eine temporäre Datei (`.crswap`) im selben Ordner und übernimmt sie erst beim `close()`.
- **Umbenennen:** über `move()`, falls für Ordner auf der Platte verfügbar. Sonst neue Datei schreiben und alte löschen.
- Eine Erkennungslogik gruppiert Dateien zu Stücken. Sie ist ein reines TypeScript-Modul ohne UI, mit Unit-Tests über viele Beispieldateinamen.

### Einstellungen und gerätespezifisches Gedächtnis

- **Gerätespezifisches Gedächtnis:** liegt in IndexedDB, der im Browser eingebauten Datenbank der App. Sie bleibt immer lokal und enthält die Ordner-Handles (Musikordner und Speicherort der Einstellungen), den zuletzt geöffneten Ordner, das Eingabegerät, die Zustände der Komponenten und Caches (z. B. Wellenformen).
- **Einstellungen:** ohne eigenen Speicherort ebenfalls in IndexedDB. Mit eigenem Speicherort als JSON-Datei in einem vom Nutzer gewählten Ordner, z. B. in einem Cloud-Ordner.
- Mit `navigator.storage.persist()` wird verhindert, dass der Browser die Daten bei Speicherknappheit selbst löscht. Löscht der Nutzer die Browserdaten, sind Gedächtnis und Ordnerfreigaben weg. Sidecar-Dateien und eine Einstellungsdatei am eigenen Speicherort bleiben erhalten.
- Export und Import betreffen nur die Einstellungen. Ein Import ersetzt sie komplett. Das Format braucht deshalb eine Versionsnummer, damit ältere Exporte später noch gelesen werden können.
- **Änderungen von außen:** Liegt die Einstellungsdatei in einem Cloud-Ordner, können zwei Rechner gleichzeitig schreiben. Die App schreibt atomar und erkennt Änderungen über `FileSystemObserver`, falls verfügbar, sonst durch Abfragen von `lastModified`.

## Weitere Festlegungen

- **Tastenkürzel** wirken global im Fenster, unabhängig vom Fokus, außer in Textfeldern. Die Belegung ist frei änderbar und wird aus den Einstellungen geladen, statt fest im Code zu stehen.
- **Mehrsprachigkeit von Anfang an:** Kein Text steht fest im Code, alle Texte kommen aus Übersetzungsdateien im JSON-Format. Sie sind für Mitwirkende ohne Programmierkenntnisse bearbeitbar.
- **Ton- und Akkordnamen** erzeugt eine eigene, gut testbare Komponente, unabhängig von der Sprache der Oberfläche. Intern rechnet die App nur mit Tonhöhen und Akkordtypen, die Namen entstehen erst bei der Anzeige.
- **Struktur gegen Wildwuchs:** Fachlogik (Gruppierung, Ton- und Akkordnamen, Einstellungsformat) liegt in reinen Modulen ohne UI und wird mit Vitest getestet. Audio ist eine eigene, abgekapselte Schicht. Die Svelte-Komponenten enthalten nur Darstellung und Bedienung.

## Prüfpunkte für den Prototyp

1. **Ordnerberechtigung:** Bleibt der Zugriff nach einem Neustart dauerhaft erhalten, im Browser und als installierte PWA, unter Linux und Windows?
2. **AAC unter Linux:** Spielen die gängigen Chromium-Builds der Distributionen M4A/AAC ab?
3. **Speicher bei langen Stücken:** `decodeAudioData` legt das ganze Stück unkomprimiert in den Speicher (10 Minuten Stereo ≈ 200 MB). Reicht das, oder braucht es eine Verarbeitung in Teilen?
4. **`FileSystemObserver`:** Ist die API in aktuellen Chromium-Versionen verfügbar?
5. **Tastenkürzel:** Lassen sich `Strg+1` bis `Strg+4`, `Strg+F` und `Strg+,` in der installierten PWA abfangen?
6. **Time-Stretching:** Qualität und CPU-Last von signalsmith-stretch bei 50 % und 150 %

## Offene Punkte

- Aufbau des Projekts (Ordnerstruktur, Module)
- Format der Sidecar-Dateien und der Einstellungen im Detail
- Bibliothek für Übersetzungen
- Hosting und Veröffentlichung (z. B. GitHub Pages mit GitHub Actions)
