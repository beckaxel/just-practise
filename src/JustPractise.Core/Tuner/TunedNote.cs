using System.Globalization;

namespace JustPractise.Core.Tuner;

/// <summary>Wie Tonnamen geschrieben werden (siehe funktionen.md, „Tonbezeichnungen“).</summary>
public enum NoteNaming
{
    /// <summary>C D E F G A H, B für das englische B♭, Cis, Es usw.</summary>
    German,

    /// <summary>C D E F G A B, mit ♯ und ♭.</summary>
    English,
}

/// <summary>Kreuz oder b für die Töne zwischen den Stammtönen.</summary>
public enum Accidentals
{
    Sharp,
    Flat,
}

/// <summary>
/// Ein erkannter Ton: der nächstgelegene Halbton (als MIDI-Nummer, 69 = A4) und die Abweichung davon in Cent.
/// </summary>
/// <param name="Midi">der nächstgelegene Halbton, klingend</param>
/// <param name="Cents">Abweichung von −50 bis +50 Cent; positiv heißt zu hoch</param>
public readonly record struct TunedNote(int Midi, double Cents)
{
    private static readonly string[] GermanSharp = ["C", "Cis", "D", "Dis", "E", "F", "Fis", "G", "Gis", "A", "Ais", "H"];
    private static readonly string[] GermanFlat = ["C", "Des", "D", "Es", "E", "F", "Ges", "G", "As", "A", "B", "H"];
    private static readonly string[] EnglishSharp = ["C", "C♯", "D", "D♯", "E", "F", "F♯", "G", "G♯", "A", "A♯", "B"];
    private static readonly string[] EnglishFlat = ["C", "D♭", "D", "E♭", "E", "F", "G♭", "G", "A♭", "A", "B♭", "B"];

    /// <summary>
    /// Der nächstgelegene Ton zu <paramref name="frequency"/> bei Kammerton <paramref name="referenceHz"/> (A4).
    /// </summary>
    public static TunedNote FromFrequency(double frequency, double referenceHz = 440)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(frequency);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(referenceHz);
        var exact = 69 + (12 * Math.Log2(frequency / referenceHz));
        var midi = (int)Math.Round(exact, MidpointRounding.AwayFromZero);
        return new TunedNote(midi, (exact - midi) * 100);
    }

    /// <summary>
    /// Der Ton, wie er in den Noten eines transponierenden Instruments steht. Ein Instrument in B klingt einen
    /// Ganzton tiefer als notiert; notiert ist also klingend + 2 Halbtöne. Die Abweichung in Cent bleibt gleich.
    /// </summary>
    /// <param name="semitones">wie viele Halbtöne der notierte Ton über dem klingenden liegt, z. B. 2 für B</param>
    public TunedNote Written(int semitones) => this with { Midi = Midi + semitones };

    /// <summary>Oktave in wissenschaftlicher Schreibweise: A4 = 440 Hz, C4 = eingestrichenes C.</summary>
    public int Octave => (int)Math.Floor(Midi / 12.0) - 1;

    /// <summary>Der Tonname ohne Oktave, z. B. „Fis“ oder „F♯“.</summary>
    public string Name(NoteNaming naming, Accidentals accidentals)
    {
        var index = ((Midi % 12) + 12) % 12;
        var names = (naming, accidentals) switch
        {
            (NoteNaming.German, Accidentals.Sharp) => GermanSharp,
            (NoteNaming.German, Accidentals.Flat) => GermanFlat,
            (NoteNaming.English, Accidentals.Sharp) => EnglishSharp,
            _ => EnglishFlat,
        };
        return names[index];
    }

    /// <summary>Die Abweichung als Text, z. B. „+3“, „−12“ oder „0“, in ganzen Cent.</summary>
    public string CentsText
    {
        get
        {
            var rounded = (int)Math.Round(Cents, MidpointRounding.AwayFromZero);
            return rounded switch
            {
                > 0 => "+" + rounded.ToString(CultureInfo.InvariantCulture),
                < 0 => "−" + (-rounded).ToString(CultureInfo.InvariantCulture),
                _ => "0",
            };
        }
    }
}
