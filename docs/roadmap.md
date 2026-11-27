# Roadmap zum Release Candidate

Ziel ist, schnell eine erste Version zu veröffentlichen und sie danach schrittweise auszubauen. Die erste Version enthält deshalb nur einen kleinen Teil des [Funktionskonzepts](funktionen.md).

## Stand (November 2026)

- **Erste Version umgesetzt:** Alle Meilensteine sind erledigt, die Release Candidates 0.1.0-rc.1 bis rc.5 sind veröffentlicht. M4A/AAC ist danach dazugekommen und erscheint mit dem nächsten Release Candidate.
- **Alle Prüfpunkte abgeschlossen:** Die [Prüfpunkte 1 bis 10](technologie.md#prüfpunkte-für-den-prototyp) sind unter Debian und Windows geprüft. Mangels Gerät ungeprüft und der Community überlassen bleiben: macOS, ein Stift unter Linux und der Start auf einem ARM-Gerät.

## Umfang der ersten Version

- **Dateibrowser:** einen oder mehrere Musikordner wählen und frei durch die Unterordner navigieren. Die Stücke werden automatisch gruppiert. Die App liest einen Ordner beim Öffnen neu ein, dazu gibt es einen Knopf zum Aktualisieren.
- **Sidecar-Dateien `.jps`:** Tempo und Lautstärke pro Audiodatei, nach den Regeln aus der [Datenhaltung](technologie.md#datenhaltung). Unbekannte Felder bleiben also erhalten, und die Datei entsteht erst bei der ersten Änderung.
- **Player:**
  - Dateiformate MP3, WAV, FLAC, OGG und M4A/AAC (AAC und ALAC über ein mitgeliefertes, schlankes FFmpeg, Prüfpunkt 4)
  - Abspielen und Pause
  - Fortschrittsbalken mit Zeitangabe, ein Klick springt an die Stelle
  - Knopf und Taste „zum Anfang“, am Ende hält die Wiedergabe an
  - Tempo 50 % bis 150 % mit signalsmith-stretch
  - Lautstärke
- **Metronom:**
  - Tempo 30 bis 250 BPM
  - Schläge pro Takt 1 bis 16, der erste betont
  - Tempo-Voreinstellungen 60, 90, 120 BPM
  - betonte Eins (fest)
  - Schlaganzeige
  - eigene Lautstärke, unabhängig vom Player
  - Die letzte Einstellung wird gemerkt.
- **Übetimer:** Countdown von 1 bis 60 Minuten mit Voreinstellungen, ohne Geräusche, Zustände über Farben, im Werkzeugbereich
- **Stimmgerät:** chromatisch, Balken für die Abweichung im Werkzeugbereich, Stimmung C/B/Es/F/A, Kammerton, Eingabegerät, Vorzeichen; Mikrofon nur offen, wenn eingeschaltet
- **Rahmen:**
  - Oberfläche auf Deutsch und Englisch
  - Tastenkürzel für Player und Metronom nach der [Standardbelegung](funktionen.md#standardbelegung), noch nicht änderbar
  - Zähler für verspätete Callbacks (siehe Prüfpunkt 1), nur im Entwickler-Modus
- **Pakete:** Windows und Linux, jeweils x64

Über den ursprünglichen Umfang hinaus sind dazugekommen: die Bibliothek als Baum (Ordner entfernen, wahlweise samt Sidecar-Dateien), das Layout mit Pfadzeile, einklappbarer Bibliothek, Werkzeugbereich und Übungsbereich, Bedienung per Touch, Vollbild, gemerkte Fensterlage, kein Bildschirmschoner bei maximiertem Fenster, nur eine laufende Instanz, Desktop-Eintrag unter Linux, Updates über Velopack und „Über just practise“ mit den Lizenzen.

## Später

- Player: Wellenform, A-B-Loop
- Dateibrowser: Dateiüberwachung (geklärt in Prüfpunkt 8, Umsetzung siehe [Datenhaltung](technologie.md#dateizugriff)), Gruppierung von Hand korrigieren, Suche, Markierung nicht erreichbarer Ordner
- Metronom: Tap-Tempo, Betonung pro Schlag, Unterteilungen, Klangsets, benannte Einstellungen
- Tastenkürzel änderbar, eigener Speicherort sowie Export und Import der Einstellungen
- Stimmgerät: Nadel und Cent in einer größeren Ansicht, eigene Instrumente, freie Halbtonverschiebung, Verlauf der Lautstärke (der Balken für den aktuellen Pegel ist schon da); Tonbezeichnung unabhängig von der Sprache, auch Solmisation
- Pakete für arm64 und macOS: Die CI baut und testet die nativen Bibliotheken dort schon (Prüfpunkt 10); ob die App startet und spielt, prüft mangels Gerät die Community.
- Schnellerer Start unter Windows: das Audiogerät im Hintergrund öffnen. WASAPI braucht dafür rund 400 ms auf dem UI-Thread, unter Linux sind es 80 ms (Prüfpunkt 9).
- Audio-Thread unter Windows bei MMCSS anmelden, erst nach einer Messung (siehe [Offene Punkte](technologie.md#offene-punkte))

Touch ist mit der ersten Version erledigt (Prüfpunkt 6). Der Stift wird erst mit der [Notenanzeige](funktionen.md#zukünftige-funktion-notenanzeige) gebraucht, für Anmerkungen.

## Meilensteine

Alle fünf Meilensteine der ersten Version sind erledigt.

1. **Core:** Gruppierungslogik, Modelle für Sidecar, Einstellungen und das gerätespezifische Gedächtnis, Taktlogik des Metronoms. Alles mit Tests.
2. **Storage:** Ordner durchlaufen sowie Sidecar-Dateien und Einstellungen atomar lesen und schreiben.
3. **Audio:** Engine und Player bauen auf den Prototypen auf. Der Player decodiert ganz in Blöcke aus einem Pool, dazu kommen Tempo, Lautstärke und Springen. Außerdem der Metronom-Klick und der Zähler für verspätete Callbacks.
4. **Oberfläche:** Dateibrowser, Player und Metronom, Sprachdateien Deutsch und Englisch, Tastenkürzel.
5. **Release Candidate:**
   - Die CI baut die native Bibliothek und führt die Tests aus.
   - Prüfpunkt 9 (Startzeit und Paketgröße mit ReadyToRun)
   - Pakete mit Velopack für Windows und Linux x64, Windows unsigniert, Linux mit SHA-256
   - Tag für den Release Candidate
