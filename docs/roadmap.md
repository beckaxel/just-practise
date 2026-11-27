# Roadmap zum Release Candidate

Ziel ist, schnell eine erste Version zu veröffentlichen und sie danach schrittweise auszubauen. Die erste Version enthält deshalb nur einen kleinen Teil des [Funktionskonzepts](funktionen.md). Die Technik dafür ist in den Prüfpunkten 1, 2, 3 und 7 der [Technologie](technologie.md#prüfpunkte-für-den-prototyp) geklärt.

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

## Später

- Player: Wellenform, A-B-Loop
- Dateibrowser: Gruppierung von Hand korrigieren, Suche, Markierung nicht erreichbarer Ordner, Dateiüberwachung (Prüfpunkt 8)
- Metronom: Tap-Tempo, Betonung pro Schlag, Unterteilungen, Klangsets, benannte Einstellungen
- Tastenkürzel änderbar, eigener Speicherort sowie Export und Import der Einstellungen
- Stimmgerät: Nadel und Cent in einer größeren Ansicht, eigene Instrumente, freie Halbtonverschiebung, Lautstärkeanzeige; Tonbezeichnung unabhängig von der Sprache
- Touch und Stift (Prüfpunkt 6)
- macOS und arm64 (Prüfpunkt 10)
- Schnellerer Start unter Windows: das Audiogerät im Hintergrund öffnen. WASAPI braucht dafür rund 400 ms auf dem UI-Thread, unter Linux sind es 80 ms (Prüfpunkt 9).

## Meilensteine

1. **Core:** Gruppierungslogik, Modelle für Sidecar, Einstellungen und das gerätespezifische Gedächtnis, Taktlogik des Metronoms. Alles mit Tests.
2. **Storage:** Ordner durchlaufen sowie Sidecar-Dateien und Einstellungen atomar lesen und schreiben.
3. **Audio:** Engine und Player bauen auf den Prototypen auf. Der Player decodiert ganz in Blöcke aus einem Pool, dazu kommen Tempo, Lautstärke und Springen. Außerdem der Metronom-Klick und der Zähler für verspätete Callbacks.
4. **Oberfläche:** Dateibrowser, Player und Metronom, Sprachdateien Deutsch und Englisch, Tastenkürzel.
5. **Release Candidate:**
   - Die CI baut die native Bibliothek und führt die Tests aus.
   - Prüfpunkt 9 (Startzeit und Paketgröße mit ReadyToRun)
   - Pakete mit Velopack für Windows und Linux x64, Windows unsigniert, Linux mit SHA-256
   - Tag für den Release Candidate
