# Technologiekonzept

> Arbeitsstand, noch nicht versioniert. Die fachlichen Anforderungen stehen in [funktionen.md](funktionen.md).

## Status

Es ist noch keine Technologie-Entscheidung getroffen. Die `.gitignore` im Repository stammt aus der .NET-Vorlage, ist aber keine Festlegung.

## Anforderungen an die Technologie

### Aus dem Funktionskonzept

- **Plattformen:** läuft unter Linux und Windows. macOS darf nicht verbaut werden.
- **Ohne Installation:** portable Auslieferung, z. B. als einzelne Datei oder Ordner, der direkt gestartet wird
- **Keine vorausgesetzte Laufzeitumgebung:** Der Nutzer muss nichts vorab installieren, etwa ein .NET Runtime oder Java.
- **Offline:** keine Abhängigkeit von Netzwerk oder Online-Diensten
- **Schneller Start**
- **Touch-Bedienung:** Das UI-Framework muss Touch-Eingaben unter Linux und Windows unterstützen, z. B. Tippen, Ziehen und Wischen.

### Aus dem Open-Source-Ansatz

- Kostenlose Werkzeuge: Beitragen geht ohne kostenpflichtige Lizenzen oder IDEs
- Entwickeln ist unter Linux, Windows und macOS gleichermaßen möglich
- Einfacher Einstieg: klonen, bauen, starten in wenigen Schritten
- Verbreitete Sprache bzw. verbreitetes Framework, damit möglichst viele beitragen können
- Abhängigkeiten müssen lizenzrechtlich mit MIT verträglich sein

### Aus dem Audioplayer

- Decodieren von MP3, WAV, FLAC, OGG und M4A/AAC
- Audioausgabe mit geringer Latenz auf allen Zielplattformen
- Time-Stretching (50–150 %) ohne Tonhöhenänderung, z. B. über eine Bibliothek wie SoundTouch oder Rubber Band. Die Lizenzen sind zu prüfen: SoundTouch steht unter LGPL, Rubber Band unter GPL oder einer kommerziellen Lizenz.
- Berechnen und Rendern einer Wellenform, auch bei langen Stücken
- Dateisystemzugriff für den Dateibrowser und die Sidecar-Dateien

### Aus künftigen Funktionen

Diese Anforderungen sind noch nicht für Schritt 1 nötig. Die Technologie sollte sie aber nicht ausschließen.

- Stimmgerät: Audio-Eingang mit wählbarem Eingabegerät, Tonhöhenerkennung in Echtzeit (z. B. YIN- oder McLeod-Verfahren, selbst umgesetzt oder als Bibliothek) und eine flüssig animierte Nadel
- Notenanzeige: PDFs und Bilder rendern, z. B. über PDFium (BSD-Lizenz) oder MuPDF (AGPL, Lizenz kritisch)
- Overlay mit Notizen über den Noten: Zeichnen bzw. Anmerkungen auf einer gerenderten Seite

## Festlegungen mit technischer Auswirkung

- Pro Stück gibt es eine Sidecar-Datei, benannt nach dem erkannten Titel (z. B. `Autumn Leaves.jp`). Der Inhalt ist JSON.
- Eine Erkennungslogik gruppiert Dateien zu Stücken. Sie sollte gut testbar sein, z. B. als eigenständige Komponente mit Unit-Tests über viele Beispieldateinamen.
- App-Einstellungen: Beim portablen Betrieb liegen sie neben der App, sonst am Standardort des Betriebssystems (`~/.config/...` bzw. `%APPDATA%`).
- Es gibt zwei getrennte Dateien: Einstellungen (exportierbar, Ort frei wählbar) und gerätespezifisches Gedächtnis (bleibt immer lokal, enthält auch den Verweis auf den gewählten Speicherort).
- Export und Import betreffen nur die Einstellungsdatei. Ein Import ersetzt sie komplett. Das Format braucht deshalb eine Versionsnummer, damit ältere Exporte später noch gelesen werden können.
- Tastenkürzel wirken global im Fenster, unabhängig vom Fokus, außer in Textfeldern. Die Belegung ist frei änderbar und muss daher aus den Einstellungen geladen werden, statt fest im Code zu stehen.
- Mehrsprachigkeit von Anfang an: Kein Text steht fest im Code, alle Texte kommen aus Übersetzungsdateien. Das Format sollte für Mitwirkende ohne Programmierkenntnisse bearbeitbar sein.
- Ton- und Akkordnamen erzeugt eine eigene, gut testbare Komponente, unabhängig von der Sprache der Oberfläche. Intern rechnet die App nur mit Tonhöhen und Akkordtypen, die Namen entstehen erst bei der Anzeige.
- Liegt der Speicherort in einem Cloud-Ordner, können zwei Rechner gleichzeitig schreiben. Die App sollte die Datei atomar schreiben (in eine temporäre Datei schreiben, dann umbenennen) und Änderungen von außen erkennen.

## Offene Punkte

- Auswahl von Sprache und UI-Framework
- Auswahl der Audio-Bibliotheken (Decoding, Ausgabe, Time-Stretching)
- Format und Ablage der Sidecar-Dateien und der App-Einstellungen
- Build und Auslieferung der portablen Versionen
