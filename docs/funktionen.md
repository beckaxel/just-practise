# Funktionskonzept

> Arbeitsstand, noch nicht versioniert. Technische Aspekte stehen in [technologie.md](technologie.md).

## Vision

**just practise** unterstützt Menschen, die ein Musikinstrument spielen, beim Üben. Die App ist ein Open-Source-Projekt: Jeder darf sie verwenden, und jeder kann mitwirken.

**Ohne Geld:** Die App ist kostenlos und verursacht keine Kosten. Es gibt keine Werbung, keine Spenden und keine kostenpflichtigen Dienste. Niemand muss jemals Geld für das Projekt verwalten.

## Leitprinzip: schnell und einfach

Wer übt, soll sofort loslegen können. Alle Hürden müssen minimal sein.

- Kein Konto, keine Anmeldung
- Komplett offline lauffähig. Die App nimmt von sich aus keinen Kontakt nach außen auf. Nach Updates sucht sie nur auf Knopfdruck.
- Einfach zu installieren, ohne weitere Voraussetzungen. Es gibt auch eine portable Variante ohne Installation.
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
| `Pos1` | zum Anfang |
| `←` / `→` | 5 Sekunden zurück/vor |
| `A` / `B` | Loop-Start/-Ende an der aktuellen Position setzen |
| `L` | Loop an/aus |
| `[` / `]` | Tempo −/+ 5 % (gilt über das Zeichen, auf der deutschen Tastatur also AltGr+8/9) |
| `↑` / `↓` | Lautstärke +/− 1 dB |

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
- **Erweiterbar:** Weitere Sprachen kommen über Übersetzungsdateien hinzu, z. B. als Beitrag aus der Community. Eine eigene Übersetzungsdatei kann man in einen Ordner der App legen und sofort ausprobieren.
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

## Bildschirmschoner

Beim Üben gibt es oft lange keine Eingabe, obwohl man auf die App schaut, z. B. auf den Fortschrittsbalken oder die Schlaganzeige. Solange das Hauptfenster maximiert ist (später auch im Vollbild), gehen deshalb Bildschirmschoner, Abschalten des Bildschirms und automatische Sperre nicht an. In normaler Größe oder minimiert gelten wieder die Einstellungen des Systems.

## Zielplattformen

- **Desktop:** Linux, Windows und macOS als native App. Die Begründung steht in [technologie.md](technologie.md).
- **Nicht geplant:** Mobile (Android/iOS). Der Dateibrowser setzt freien Zugriff auf Ordner voraus, den es dort nicht gibt.

## Roadmap

1. Audioplayer
2. Übetimer
3. Metronom
4. Stimmgerät

Welche Teile davon in die erste Version kommen, steht in der [Roadmap zum Release Candidate](roadmap.md).

## Schritt 1: Audioplayer

### Funktionsumfang

- **Dateiformate:** MP3, WAV, FLAC, OGG, M4A/AAC
- **Visualisierung:** Wellenform des Stücks
- **Wiedergabe:** Abspielen, Pause, Positionieren in der Wellenform. Eine gewählte Datei lädt angehalten am Anfang. Am Ende hält die Wiedergabe an; Abspielen fängt dann von vorn an.
- **Loop:** ein einfacher A-B-Loop
- **Tempo:** 50 % bis 150 % in ganzen Prozent, ohne Änderung der Tonhöhe
- **Lautstärke:** individuell pro Audiodatei und gespeichert, −20 bis +10 dB in Schritten von 0,5 dB. Unterschiedlich ausgesteuerte Aufnahmen muss man so nicht jedes Mal neu anpassen.
- **Zurücksetzen:** Ein Klick auf den angezeigten Wert von Tempo oder Lautstärke setzt ihn auf 100 % bzw. 0 dB zurück.
- **Ohne Ausgabegerät:** Die App startet trotzdem und läuft ohne Ton; die Statusleiste weist darauf hin. Die Statusleiste zeigt sonst das Ausgabegerät und, nur wenn es welche gab, die Zahl der verspäteten Audio-Callbacks.

### Dateibrowser

Die App funktioniert wie ein Dateibrowser: Man navigiert durch Ordner und öffnet Stücke direkt. Es gibt keinen Import und keine Bibliothek.

- **Musikordner:** Beim ersten Start wählt man einen Ordner, z. B. den Musik-Ordner. Weitere Ordner lassen sich jederzeit hinzufügen, z. B. einen Ordner auf einer zweiten Platte. Innerhalb der Musikordner navigiert man frei durch alle Unterordner.
- **Startpunkt:** der zuletzt geöffnete Ordner
- **Ordner nicht erreichbar:** Ist ein Musikordner nicht erreichbar (z. B. externe Platte nicht angeschlossen), zeigt die App ihn weiter an, markiert ihn und greift wieder darauf zu, sobald er da ist.
- **Anzeige:** Ordner und Stücke (siehe unten). Andere Dateien und die Sidecar-Dateien sind ausgeblendet.
- **Aufbau:** Der Browser zeigt immer einen Ordner, mit einer Pfadzeile zum Zurückspringen: oben die Unterordner, darunter die Stücke. Auf der obersten Ebene stehen die Musikordner. Ein Stück mit mehreren Audiodateien lässt sich im Browser aufklappen; ein Klick auf das Stück lädt seine erste Audiodatei, ein Klick auf eine Datei darunter genau diese. Aufgeklappt stehen die vollen Dateinamen mit Endung da, z. B. `Autumn Leaves - Playalong 80.mp3`. Auf der obersten Ebene gibt es einen Knopf, um einen weiteren Musikordner hinzuzufügen; beim ersten Start steht dort stattdessen ein Hinweis mit dem Knopf „Musikordner wählen“. Entfernen lassen sich Musikordner in den Einstellungen.

### Stücke und Gruppierung

Ein **Stück** fasst alle Dateien zusammen, die dazugehören. Beispiele:

- mehrere Audiodateien, z. B. Demo, Playalong oder Aufnahmen in verschiedenen Tempi
- künftig Noten als PDF oder Bild

Im Dateibrowser erscheinen die Dateien eines Stücks immer gruppiert als ein Eintrag.

Feste Benennungsregeln gibt es bewusst nicht. Eine Erkennungslogik ordnet die Dateien den Stücken zu. Ausgangsidee: Dateien, deren Name gleich beginnt, gehören zusammen. Die Logik muss aber verschiedene Titel sicher unterscheiden können, damit nichts zusammengefasst wird, was nicht zusammengehört.

Beispiel:

| Datei                               | Stück             |
|-------------------------------------|-------------------|
| `Autumn Leaves - Demo.mp3`          | Autumn Leaves     |
| `Autumn Leaves - Playalong 80.mp3`  | Autumn Leaves     |
| `Autumn Leaves.pdf`                 | Autumn Leaves     |
| `Blues.mp3`                         | Blues             |
| `Blues in F.mp3`                    | **Blues in F**, nicht „Blues“ |

Regeln der Erkennung (abgeleitet aus einer echten Sammlung von rund 2500 Dateien):

- **Vergleich:** Groß- und Kleinschreibung zählen nicht, Apostrophe (`'`, `’`, `´`) werden ignoriert, Unicode wird vereinheitlicht. `Aint no Sunshine` und `Ain't No Sunshine` sind also gleich.
- **Nummern am Anfang** werden für den Vergleich abgetrennt, z. B. `01 `, `001 - `, `01_`, `1-01 `, `45. `, `Track 01 `, `Titel 01 `.
- **Zusätze am Ende** werden abgetrennt, solange sie bekannt sind. Ein Leerzeichen reicht als Trennung (`August Song Play-Along`), Zahlen dürfen angeklebt sein (`Blue Bossa100`, `60bpm`). Bekannt sind:
  - Playalong und Begleitung, z. B. Play-Along, Playback, PB, Bck, bckgr, (Rhythm Section Only), (ohne Sax), no piano, only bs and drums
  - Demo und Vollversion, z. B. Demo, Instr, (mit Sax), full version, complete, orig
  - Tempo: eine Zahl von 30 bis 400, `80 bpm`, `Tempo 66`, fast, slow. Eine Zahl, die schon in der Nummer am Anfang steht, ist kein Tempo (`30 Titel 30`).
  - Metronom und Klick
  - Stimmung und Instrument als nacktes Kürzel: Bb, Eb, Es, C, Ces, Alto, Alt, Tenor, höchstens eines pro Name (in `Skalen Akk C Eb` ist das C die Tonleiter). Nach „in“ bleibt die Tonart Teil des Titels (`Blues in F`), ebenso Angaben wie `G-Dur`.
  - Kleine Zahlen unter 30 bleiben Teil des Titels (`Impro 4`, `Trommlermarsch 01`). Bleibt nach dem Abtrennen nichts übrig (`Titel 01 click 48`), gilt der ganze Name.
- **Verschiedene Nummern:** Dateien mit verschiedenen Nummern gehören nur zusammen, wenn sie sich durch Zusätze unterscheiden (`02 August Song` und `03 August Song Play-Along`). Gleiche Namen mit verschiedenen Nummern sind verschiedene Stücke (`003 - Bourrée` und `006 - Bourrée`, `001 PianoFitness` bis `111 PianoFitness`).
- **Harte Trennzeichen** (` - `, `_`, `(`, `[`): Danach darf beliebiger Text folgen, aber nur, wenn es eine Datei gibt, die genau wie der Teil davor heißt. `Hot Cross Buns - 01 - Metronom` gehört so zu `Hot Cross Buns`, aus `Franz Titscher - Kinderlieder` und `Franz Titscher - Weihnachtslieder` wird aber kein Stück „Franz Titscher“.
- **Titel:** Der Titel eines Stücks ist der Name seiner Grunddatei (der Datei ohne Zusätze) bis zum Ende des erkannten Titels, mit der Nummer am Anfang, z. B. `02 August Song`. So bleiben die Titel im Ordner eindeutig und passen zur Nummerierung im Heft.
- Vorerst werden nur Audiodateien gruppiert. Noten und andere Dateien kommen mit der Notenanzeige dazu.
- Die Listen der Zusätze werden bei Bedarf ergänzt.

**Korrektur von Hand:** Der Nutzer kann eine falsche Gruppierung korrigieren. Die Sidecar-Datei hält dann fest, welche Dateien zum Stück gehören, und hat Vorrang vor der automatischen Erkennung. Ohne Sidecar wird automatisch gruppiert.

### Sidecar-Dateien

Pro Stück gibt es eine Sidecar-Datei. Sie liegt neben den Dateien des Stücks. So bleiben die Daten beim Stück, auch wenn man den Ordner kopiert oder auf einen anderen Rechner mitnimmt. Eine zentrale Datenbank gibt es nicht.

- **Name:** der erkannte Titel des Stücks, z. B. `Autumn Leaves.jps`
- **Dateiendung:** `.jps`, der Inhalt ist JSON
- **Inhalt:**
  - Einstellungen pro Audiodatei des Stücks: A-B-Loop, Tempo, Lautstärke
  - falls von Hand korrigiert: die Liste der Dateien, die zum Stück gehören
  - künftig: Notizen zu den Noten
- **Anlage:** erst bei der ersten Änderung, nicht schon beim Öffnen. Ordner mit Stücken, die man nur angehört hat, bleiben sauber.

### App-Einstellungen

Die App speichert zwei Arten von Daten getrennt:

- **Einstellungen:** alles, was der Nutzer einstellt, z. B. Timerdauer, letzte und benannte Metronom-Einstellungen, Kammerton
- **Gerätespezifisches Gedächtnis:** was nur auf diesem Rechner Sinn ergibt, z. B. die Musikordner, zuletzt geöffneter Ordner, Zustand der Komponenten (ausgeklappt, eingeklappt, geschlossen), das Eingabegerät des Stimmgeräts und der gewählte Speicherort

#### Speicherort

- **Standard:** Beide liegen als Dateien im Ordner für App-Daten des Betriebssystems. Der Nutzer muss nichts einrichten.
- **Eigener Speicherort:** Für die Einstellungen kann man einen eigenen Ordner wählen, z. B. einen Cloud-Ordner, damit mehrere Rechner dieselben Einstellungen nutzen. Sie liegen dort als Datei. Das gerätespezifische Gedächtnis bleibt im Ordner für App-Daten und merkt sich, wo die Einstellungen liegen.
- **Wechsel:** Beim Wechsel des Speicherorts fragt die App, ob die bisherigen Einstellungen mitgenommen werden oder ob am neuen Ort neu begonnen wird. Liegen dort schon Einstellungen, werden diese verwendet.
- **Nicht erreichbar:** Ist der gewählte Ordner nicht erreichbar (z. B. USB-Stick nicht eingesteckt oder Cloud-Laufwerk nicht eingebunden), startet die App trotzdem mit Standardwerten, zeigt einen Hinweis und überschreibt nichts.

So funktioniert die App ohne Einrichtung und auf mehreren Rechnern mit denselben Einstellungen.

#### Export und Import

- **Export:** schreibt alle Einstellungen in eine einzelne Datei mit der Endung `.jpsettings`. Es wird immer alles exportiert, das gerätespezifische Gedächtnis aber nicht.
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
- **Ändern während des Laufs:** Ein neues Tempo gilt ab dem letzten Schlag, der nächste kommt also im neuen Abstand. Liegt dieser Zeitpunkt schon zurück, kommt er sofort. Die Zählung im Takt läuft weiter. Wird die Taktart kürzer und passt der nächste Schlag nicht mehr hinein, beginnt ein neuer Takt mit der Eins. Start beginnt immer mit der Eins.
- **Bezug der BPM:** Ein Schlag ist eine Zählzeit der Taktart (bei 4/4 die Viertel). Wie das bei Achteltakten wie 6/8 aussieht, wird mit den freien Taktarten entschieden.

### Speichern

- **Letzte Einstellung:** Tempo, Taktart, Betonung, Unterteilung, Klang und die drei Lautstärken werden in den [App-Einstellungen](#app-einstellungen) gespeichert. Beim nächsten Start sind sie wieder da. Ob das Metronom lief, wird nicht gemerkt; nach dem Start der App steht es.
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
- **Lautstärke:** Das Stimmgerät zeigt zusätzlich die Lautstärke des gespielten Tons. So lässt sich Dynamik üben, z. B. ein gleichmäßiges Piano oder ein kontrolliertes Crescendo (siehe [Lautstärkeanzeige](#lautstärkeanzeige)).

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

### Lautstärkeanzeige

Die Anzeige hilft beim Üben von Dynamik. Sie zeigt, wie laut man gerade spielt.

- **Skala:** relativer Pegel in dB. Ein absoluter Wert wäre nicht sinnvoll, weil er von Mikrofon, Abstand und Raum abhängt.
- **Aktueller Pegel und Verlauf:** Neben dem aktuellen Pegel zeigt ein Verlauf der letzten Sekunden, wie sich die Lautstärke entwickelt. Daran erkennt man Crescendi, Decrescendi und gleichmäßig gehaltene Töne.
- **Eingeklappte Darstellung:** ein einfacher Balken für den aktuellen Pegel
- **Eigene Komponente oder Teil des Stimmgeräts:** Das entscheidet sich beim Entwurf der Oberfläche. Lassen sich Tonhöhe, Pegel und Verlauf nicht leicht erfassbar gemeinsam darstellen, wird die Lautstärkeanzeige eine eigene Komponente. Sie bekommt dann wie alle Komponenten die Zustände ausgeklappt, eingeklappt und geschlossen, ein Symbol in der Leiste und eigene Tastenkürzel. Das Eingabegerät teilt sie sich mit dem Stimmgerät.

### Eingeklappte Darstellung

Siehe [Komponenten](#komponenten). Eingeklappt zeigt das Stimmgerät den Tonnamen in einem Rechteck. Daneben zeigt ein Strich die Abweichung: Je weiter der Ton daneben liegt, desto weiter ist der Strich vom Zentrum entfernt.
