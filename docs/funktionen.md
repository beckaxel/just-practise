# Funktionskonzept

> Arbeitsstand, noch nicht versioniert. Technische Aspekte stehen in [technologie.md](technologie.md).

## Vision

**just practise** unterstützt Menschen, die ein Musikinstrument spielen, beim Üben. Die App ist ein Open-Source-Projekt: Jeder darf sie verwenden, und jeder kann mitwirken.

## Leitprinzip: schnell und einfach

Wer übt, soll sofort loslegen können. Alle Hürden müssen minimal sein.

- Kein Konto, keine Anmeldung
- Komplett offline lauffähig
- Ohne Installation nutzbar, auch als portable Version
- Schneller Start ohne Ladebildschirm oder Onboarding
- Die wichtigste Funktion ist mit einem Klick erreichbar
- Sinnvolle Standardwerte; Einstellungen sind optional

## Bedienung

Die App ist mit Maus und Tastatur bedienbar. Wenn ein Touch-Display vorhanden ist, lässt sie sich grundsätzlich auch per Touch bedienen. Bedienelemente müssen dafür groß genug sein, und keine Funktion darf nur per Hover oder Rechtsklick erreichbar sein.

## Tastenkürzel

Alle wichtigen Funktionen sind über Tastenkürzel erreichbar.

- **Immer gleich:** Ein Kürzel wirkt immer gleich, egal welche Komponente gerade den Fokus hat.
- **Ausnahme Texteingabe:** Solange man in ein Textfeld schreibt (z. B. Suche, BPM-Eingabe, Name einer Einstellung), gehen die Tasten in das Feld. Kürzel ohne `Strg` wirken dann nicht. `Esc` verlässt das Feld.
- **Eigene Belegung:** Jedes Kürzel lässt sich in den Einstellungen ändern. Die App warnt, wenn eine Taste schon belegt ist. Man kann jederzeit zur Standardbelegung zurückkehren. Die Belegung liegt in den [App-Einstellungen](#app-einstellungen) und wird mit exportiert.

### Standardbelegung

**Global**

| Taste | Aktion |
|---|---|
| `1` bis `4` | Audioplayer, Übetimer, Metronom, Stimmgerät öffnen oder schließen |
| `Strg+1` bis `Strg+4` | Komponente ein- oder ausklappen |
| `Strg+F` | Suche im Dateibrowser |
| `Strg+,` | Einstellungen |

**Audioplayer**

| Taste | Aktion |
|---|---|
| `Leertaste` | Abspielen/Pause |
| `←` / `→` | 5 Sekunden zurück/vor |
| `A` / `B` | Loop-Start/-Ende an der aktuellen Position setzen |
| `L` | Loop an/aus |
| `[` / `]` | Tempo −/+ 5 % |
| `↑` / `↓` | Lautstärke +/− |

**Übetimer**

| Taste | Aktion |
|---|---|
| `T` | Start/Pause |
| `Umschalt+T` | Reset |
| `Umschalt+↑` / `Umschalt+↓` | Dauer +/− 1 Minute |

**Metronom**

| Taste | Aktion |
|---|---|
| `M` | Start/Stopp |
| `+` / `−` | Tempo +/− 1 BPM |
| `Umschalt++` / `Umschalt+−` | Tempo +/− 10 BPM |
| `Enter` | Tap-Tempo |

**Stimmgerät**

| Taste | Aktion |
|---|---|
| `S` | an/aus |
| `I` | nächstes Instrument |

## Sprache und Musiknotation

Sprache der Oberfläche, Tonbezeichnungen und Akkordsymbole sind drei getrennte Einstellungen. Wer z. B. eine englische Oberfläche nutzt, kann trotzdem deutsche Tonnamen mit H wählen. Alle drei liegen in den [App-Einstellungen](#app-einstellungen) und werden mit exportiert.

### Sprache der Oberfläche

- **Zum Start:** Deutsch und Englisch
- **Erweiterbar:** Weitere Sprachen kommen über Übersetzungsdateien hinzu, z. B. als Beitrag aus der Community.
- **Erster Start:** Die App übernimmt die Sprache des Betriebssystems. Gibt es diese nicht, nimmt sie Englisch.
- **Umstellen:** jederzeit in den Einstellungen

### Tonbezeichnungen

- **Deutsch:** C D E F G A H, B für das englische B♭, Formen wie Cis und Es
- **Englisch:** C D E F G A B, mit C♯ und B♭
- **Solmisation mit festem Do:** Do Re Mi Fa Sol La Si
- **Vorzeichen:** wahlweise Kreuz oder b. Das betrifft z. B. das Stimmgerät: Cis oder Des.
- **Standard:** passend zur Sprache der Oberfläche, also Deutsch oder Englisch. Man kann es unabhängig davon umstellen.
- Tonbezeichnungen, die von der Tonart abhängen (bewegtes Do, Nashville Number System), gibt es nicht. Sie passen nicht zu einem chromatischen Stimmgerät.

### Akkordsymbole

Noch zeigt keine Komponente Akkorde an. Die App wird aber jetzt schon darauf vorbereitet, damit spätere Funktionen (z. B. Akkorde als Notizen über den Noten) die Einstellung nutzen können.

- **Schreibweisen:** einstellbar, z. B. Cmaj7 / CΔ7 / Cj7, Cm7 / C-7 / Cmi7, Cø / Cm7♭5 oder C° / Cdim
- **Grundtöne:** folgen der gewählten Tonbezeichnung

## Komponenten

Die App besteht aus Komponenten: Audioplayer, Übetimer, Metronom, Stimmgerät und künftig die Notenanzeige. Jede Komponente hat einen eigenen Bereich in der Oberfläche und kann drei Zustände haben:

- **ausgeklappt:** volle Darstellung und alle Bedienelemente
- **eingeklappt:** minimalistische Darstellung mit dem Wichtigsten
- **geschlossen:** nicht sichtbar, für Nutzer, die die Komponente gerade nicht oder noch nicht brauchen

Was die minimalistische Darstellung zeigt, legt jede Komponente selbst fest.

- **Dateibrowser:** Er ist keine Komponente, sondern das feste Grundgerüst der App. Er ist immer sichtbar.
- **Ein- und ausschalten:** Eine kleine Leiste zeigt ein Symbol pro Komponente. Mit einem Klick oder Tipp öffnet oder schließt man die Komponente. Jede Komponente lässt sich schließen, auch der Audioplayer, z. B. wenn man nur Timer und Metronom nutzt.
- **Merken:** Der Zustand jeder Komponente wird in den App-Einstellungen gespeichert und beim nächsten Start wiederhergestellt.

## Zielplattformen

- **Jetzt:** Desktop unter Linux und Windows
- **Später:** macOS, eventuell durch Beiträge aus der Community
- **Nicht geplant:** Mobile (Android/iOS)

## Roadmap

1. Audioplayer
2. Übetimer
3. Metronom
4. Stimmgerät

## Schritt 1: Audioplayer

### Funktionsumfang

- **Dateiformate:** MP3, WAV, FLAC, OGG, M4A/AAC
- **Visualisierung:** Wellenform des Stücks
- **Wiedergabe:** Abspielen, Pause, Positionieren in der Wellenform
- **Loop:** ein einfacher A-B-Loop
- **Tempo:** 50 % bis 150 %, ohne Änderung der Tonhöhe

- **Lautstärke:** individuell pro Stück und gespeichert. Unterschiedlich ausgesteuerte Aufnahmen muss man so nicht jedes Mal neu anpassen.

### Dateibrowser

Die App funktioniert wie ein Dateibrowser: Man navigiert durch Ordner und öffnet Stücke direkt. Es gibt keinen Import und keine Bibliothek.

- **Startpunkt:** der zuletzt geöffnete Ordner. Beim ersten Start ist das der Musik-Ordner des Nutzers, ersatzweise das Home-Verzeichnis. Man kann frei im gesamten Dateisystem navigieren.
- **Anzeige:** Ordner und Stücke (siehe unten). Andere Dateien und die Sidecar-Dateien sind ausgeblendet.

### Stücke und Gruppierung

Ein **Stück** fasst alle Dateien zusammen, die dazugehören. Beispiele:

- mehrere Audiodateien, z. B. Demo, Playalong oder Aufnahmen in verschiedenen Tempi
- künftig Noten als PDF oder Bild

Im Browser erscheinen die Dateien eines Stücks immer gruppiert als ein Eintrag.

Feste Benennungsregeln gibt es bewusst nicht. Eine Erkennungslogik ordnet die Dateien den Stücken zu. Ausgangsidee: Dateien, deren Name gleich beginnt, gehören zusammen. Die Logik muss aber verschiedene Titel sicher unterscheiden können, damit nichts zusammengefasst wird, was nicht zusammengehört.

Beispiel:

| Datei                               | Stück             |
|-------------------------------------|-------------------|
| `Autumn Leaves - Demo.mp3`          | Autumn Leaves     |
| `Autumn Leaves - Playalong 80.mp3`  | Autumn Leaves     |
| `Autumn Leaves.pdf`                 | Autumn Leaves     |
| `Blues.mp3`                         | Blues             |
| `Blues in F.mp3`                    | **Blues in F**, nicht „Blues“ |

Regeln der Erkennung:

- Ein gemeinsamer Namensanfang zählt nur, wenn er an einem Trennzeichen endet, z. B. ` - `, `_`, `(`, `[`.
- Typische Zusätze werden erkannt und gehören nicht zum Titel, z. B. „Demo“, „Playalong“, Tempoangaben wie „80 bpm“, Seitennummern.

**Korrektur von Hand:** Der Nutzer kann eine falsche Gruppierung korrigieren. Die Sidecar-Datei hält dann fest, welche Dateien zum Stück gehören, und hat Vorrang vor der automatischen Erkennung. Ohne Sidecar wird automatisch gruppiert.

### Sidecar-Dateien

Pro Stück gibt es eine Sidecar-Datei. Sie liegt neben den Dateien des Stücks. So bleiben die Daten beim Stück, auch wenn man den Ordner kopiert oder auf einen anderen Rechner mitnimmt. Eine zentrale Datenbank gibt es nicht.

- **Name:** der erkannte Titel des Stücks, z. B. `Autumn Leaves.jp`
- **Dateiendung:** `.jp`, der Inhalt ist JSON
- **Inhalt:**
  - Einstellungen pro Audiodatei des Stücks: A-B-Loop, Tempo, Lautstärke
  - falls von Hand korrigiert: die Liste der Dateien, die zum Stück gehören
  - künftig: Notizen zu den Noten
- **Anlage:** erst bei der ersten Änderung, nicht schon beim Öffnen. Ordner mit Stücken, die man nur angehört hat, bleiben sauber.

### App-Einstellungen

Die App speichert zwei Arten von Daten in getrennten Dateien:

- **Einstellungen:** alles, was der Nutzer einstellt, z. B. Timerdauer, letzte und benannte Metronom-Einstellungen, Kammerton
- **Gerätespezifisches Gedächtnis:** was nur auf diesem Rechner Sinn ergibt, z. B. Fenstergröße und -position, zuletzt geöffneter Ordner, Zustand der Komponenten (ausgeklappt, eingeklappt, geschlossen) und der gewählte Speicherort

#### Speicherort

Beim Start prüft die App, ob neben ihr eine Markierung für den portablen Betrieb liegt (z. B. `portable` oder `settings.json`).

- **Portabel:** Beide Dateien liegen neben der App. Einen eigenen Speicherort kann man dann nicht wählen.
- **Standard:** Beide Dateien liegen am Standardort des Betriebssystems (`~/.config/...` bzw. `%APPDATA%`).
- **Eigener Speicherort:** Für die Einstellungen kann man einen eigenen Ordner wählen, z. B. einen Cloud-Ordner, damit mehrere Rechner dieselben Einstellungen nutzen. Das gerätespezifische Gedächtnis bleibt am Standardort und merkt sich, wo die Einstellungen liegen.
- **Wechsel:** Beim Wechsel des Speicherorts fragt die App, ob die bisherigen Einstellungen mitgenommen werden oder ob am neuen Ort neu begonnen wird. Liegen dort schon Einstellungen, werden diese verwendet.
- **Nicht erreichbar:** Ist der gewählte Ordner nicht erreichbar (z. B. USB-Stick nicht eingesteckt), startet die App trotzdem mit Standardwerten, zeigt einen Hinweis und überschreibt nichts.

So funktioniert die App portabel, an schreibgeschützten Orten und auf mehreren Rechnern.

#### Export und Import

- **Export:** schreibt alle Einstellungen in eine einzelne Datei. Es wird immer alles exportiert, das gerätespezifische Gedächtnis aber nicht.
- **Import:** ersetzt alle aktuellen Einstellungen durch die importierte Datei. Vorher fragt die App nach. Das gerätespezifische Gedächtnis bleibt unverändert.
- Die Sidecar-Dateien der Stücke sind nicht betroffen. Sie liegen ohnehin bei den Stücken.

## Zukünftige Funktion: Notenanzeige

Noch nicht in die Roadmap eingeordnet. Muss aber schon beim Dateibrowser und bei den Sidecar-Dateien berücksichtigt werden.

- Noten eines Stücks anzeigen, abgelegt als PDF oder Bild
- Notizen in einem Overlay über den Noten, gespeichert in einer Sidecar-Datei
- **Handschriftliche Notizen:** Wenn das Gerät Touch oder Stifteingabe unterstützt, kann man Notizen direkt mit dem Finger oder Stift auf die Noten schreiben. Mit der Maus kann man ebenfalls zeichnen.
- **Textnotizen:** Notizen lassen sich auch über die Tastatur als Text auf die Noten setzen.
- **Werkzeuge:** Stift, Radierer und Textmarker. Es gibt drei Strichstärken und eine Auswahl an Farben.
- Die Notendatei heißt genauso wie die zugehörige Audiodatei, z. B. `Stück.mp3` und `Stück.pdf`.

## Schritt 2: Übetimer

Ein einfacher Countdown-Timer. Er ist komplett unabhängig vom Audioplayer und wird nur von Hand bedient.

### Leitgedanke: kein Stress

Der Timer soll beim Üben begleiten, nicht antreiben.

- Es gibt keine Sekunden, weder beim Einstellen noch in der Anzeige. Ausnahme sind die letzten 30 Sekunden.
- Der Timer macht **niemals Geräusche**. Er zeigt seinen Zustand nur über Farben.

### Funktionsumfang

- **Einstellen:** die Dauer in ganzen Minuten, von 1 bis 60
- **Standarddauer:** beim ersten Start 10 Minuten, danach die zuletzt eingestellte Dauer. Sie wird in den App-Einstellungen gespeichert.
- **Bedienung:**
  - **Start:** startet den Timer oder setzt ihn nach einer Pause fort
  - **Pause:** hält den Timer an
  - **Reset:** setzt den Timer auf die eingestellte Dauer zurück
- **Anzeige:** die verbleibenden Minuten, immer aufgerundet. Direkt nach dem Start von 25 Minuten steht also weiter „25“. In den letzten 30 Sekunden wird in 10-Sekunden-Schritten angezeigt: 30, 20, 10.
- **Ende:** Wie bei einer Sanduhr ist der Timer bei 0 abgelaufen und bleibt stehen. Es gibt kein Weiterzählen.
- **Tastenkürzel:** siehe [Tastenkürzel](#tastenkürzel)

### Zustände

Jeder Zustand hat eine eigene Farbe. Keine Farbe soll alarmieren, und nichts blinkt.

| Zustand            | Darstellung                      |
|--------------------|----------------------------------|
| bereit             | neutral                          |
| läuft              | ruhige Farbe                     |
| pausiert           | gedämpft                         |
| letzte 30 Sekunden | sanfter Hinweis                  |
| abgelaufen         | deutlich, bleibt bis zum Reset   |

### Eingeklappte Darstellung

Siehe [Komponenten](#komponenten). Eingeklappt zeigt der Timer nur die verbleibenden Minuten in der Farbe des Zustands. So bleibt der Ablauf im Blick.

## Schritt 3: Metronom

### Funktionsumfang

- **Tempo:** 30 bis 250 BPM, einstellbar per Regler, per direkter Eingabe und per Tap-Tempo
- **Tap-Tempo:** Man tippt mehrmals auf eine Schaltfläche oder Taste. Das Tempo ergibt sich aus den letzten Tipps.
- **Taktart:** frei einstellbar, z. B. auch 7/8 oder 5/4. Gängige Taktarten wie 2/4, 3/4, 4/4 und 6/8 gibt es als Schnellauswahl.
- **Betonung:** Jeder Schlag ist entweder betont, normal oder stumm. Antippen schaltet um. Standardmäßig ist die Eins betont.
- **Unterteilungen:** Achtel, Triolen, Sechzehntel, Quintolen, Sextolen und Swing-Achtel.
- **Klänge:** mitgelieferte Klangsets, z. B. Klick, Holzblock und Beep. Zusätzlich kann man eigene Audiodateien als Klänge laden.
- **Lautstärke:** Betonte Schläge, normale Schläge und Unterteilungen haben je einen eigenen Lautstärkeregler. Alle drei sind unabhängig von der Lautstärke des Audioplayers.
- **Unabhängig vom Player:** Das Metronom hat ein eigenes Tempo und einen eigenen Start/Stopp. Es läuft nicht synchron zum Stück. Eine Kopplung kann später kommen.

### Speichern

- **Letzte Einstellung:** Tempo, Taktart, Betonung, Unterteilung, Klang und die drei Lautstärken werden in den [App-Einstellungen](#app-einstellungen) gespeichert. Beim nächsten Start sind sie wieder da.
- **Benannte Einstellungen:** Zusätzlich kann man Einstellungen unter einem Namen speichern, z. B. „Etüde 7/8, 92 BPM“, und später wieder aufrufen. Auch sie liegen in den App-Einstellungen, weil sie nicht an ein Stück gebunden sind.

### Schlaganzeige

Eine Reihe Punkte zeigt die Schläge des Takts, ein Punkt pro Schlag. Der aktuelle Schlag leuchtet auf. Betonte, normale und stumme Schläge sehen unterschiedlich aus. Über die Punkte stellt man auch die Betonung ein.

### Eingeklappte Darstellung

Siehe [Komponenten](#komponenten). Eingeklappt zeigt das Metronom das Tempo in BPM, eine Start/Stopp-Schaltfläche und eine verkleinerte Schlaganzeige.

## Schritt 4: Stimmgerät

### Funktionsumfang

- **Modus:** rein chromatisch. Das Stimmgerät erkennt jeden Ton, Voreinstellungen für Instrumente gibt es nicht.
- **Kammerton:** frei wählbar in Hz, Standard 440 Hz. Er gilt global und wird in den [App-Einstellungen](#app-einstellungen) gespeichert.
- **Eingabegerät:** wählbar, z. B. eingebautes Mikrofon oder Audio-Interface. Die Auswahl gehört zum gerätespezifischen Gedächtnis, weil jeder Rechner andere Geräte hat.
- **Kein Referenzton:** Das Stimmgerät spielt keine Töne ab, es hört nur zu.

### Transponierende Instrumente

- **Stimmung:** Gängige Stimmungen gibt es als Schnellauswahl: C (klingend, Standard), B (z. B. Trompete, Klarinette, Tenorsax), Es (z. B. Altsax, Baritonsax), F (z. B. Horn, Englischhorn) und A (z. B. A-Klarinette). Zusätzlich kann man eine beliebige Verschiebung in Halbtönen eingeben.
- **Eigene Instrumente:** Wer mehrere Instrumente spielt, legt sie mit Namen und Stimmung an, z. B. „Tenorsax (B)“ oder „Altsax (Es)“. Im Stimmgerät wechselt man mit einem Klick oder Tipp zwischen ihnen. Die Liste und das zuletzt gewählte Instrument liegen in den [App-Einstellungen](#app-einstellungen).
- **Angezeigter Ton:** groß der Ton, wie er in den Noten des Instruments steht. Klein und unauffällig darunter der klingende Ton.
- **Hinweis:** Bei einer transponierenden Stimmung zeigt ein kleiner Hinweis wie „in B“, dass transponiert wird, auch in der eingeklappten Darstellung.
- **Nadel und Cent:** Sie bleiben gleich, die Transposition ändert nur den Tonnamen.
- **Kein Wechsel pro Stück:** Das Instrument wechselt vorerst nicht automatisch mit dem Stück. Es bleibt das zuletzt gewählte, bis man es selbst umstellt. Eine Kopplung an das Stück über die Sidecar-Datei kann später kommen.

### Anzeige

- **Nadel:** Sie zeigt die Abweichung vom erkannten Ton. In der Mitte ist der Ton sauber.
- **Ton und Abweichung:** Tonname und Abweichung in Cent werden als Text angezeigt.
- Weitere Anzeigearten, z. B. Balken oder Strobe, können später dazukommen.

### Eingeklappte Darstellung

Siehe [Komponenten](#komponenten). Eingeklappt zeigt das Stimmgerät den Tonnamen in einem Rechteck. Daneben zeigt ein Strich die Abweichung: Je weiter der Ton daneben liegt, desto weiter ist der Strich vom Zentrum entfernt.
