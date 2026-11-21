using System.Globalization;

namespace JustPractise.Core.Pieces;

/// <summary>
/// Bekannte Zusätze am Ende eines Dateinamens, die nicht zum Titel gehören.
/// Die Listen werden bei Bedarf ergänzt. Alle Einträge sind klein und ohne Apostrophe geschrieben,
/// <c>#</c> steht für eine Tempoangabe.
/// </summary>
internal static class Suffixes
{
    private static readonly string[][] Phrases =
    [
        .. new[]
        {
            // Playalong und Begleitung
            "play-along", "play along", "playalong", "play-a-long", "playback", "pb", "bck", "bckgr", "background",
            "backing track", "backing", "begleitung", "rhythm section only", "only bs and drums", "only bs and drs",
            "bs and drums", "bs drs git", "bs drs", "no piano", "no pn", "without piano", "ohne sax", "ohne klavier",
            "lehrerpart",
            // Demo und Vollversion
            "demo", "sax demonstration track", "demonstration", "mit sax", "mit klavier", "instr", "instrumental",
            "full version", "full", "complete", "komplett", "orig", "original",
            // Tempo
            "# bpm", "tempo #", "# t", "fast", "faster", "slow", "slower", "schnell", "langsam",
            // Metronom
            "metronom #", "metronome #", "click #", "klick #", "metronom", "metronome", "click", "klick",
        }
        .Select(phrase => phrase.Split(' '))
        .OrderByDescending(words => words.Length),
    ];

    /// <summary>Stimmung und Instrument als nacktes Kürzel. Nach „in“ gehört die Tonart zum Titel.</summary>
    private static readonly HashSet<string> Transpositions = ["bb", "eb", "es", "c", "ces", "alto", "alt", "tenor"];

    private const int MinTempo = 30;
    private const int MaxTempo = 400;

    /// <summary>
    /// Wie viele Wörter vom Anfang zum Titel gehören. Bleibt nach dem Abtrennen nichts übrig, gilt der ganze Name.
    /// </summary>
    /// <param name="tokens">die Wörter ohne Nummer am Anfang</param>
    /// <param name="trackNumbers">die Zahlen der Nummer am Anfang; eine gleiche Zahl am Ende ist kein Tempo (<c>30 Titel 30</c>)</param>
    public static int TitleLength(IReadOnlyList<NameToken> tokens, IReadOnlySet<int> trackNumbers)
    {
        var length = tokens.Count;
        var transpositionStripped = false;
        while (length > 0)
        {
            var phrase = PhraseLengthEndingAt(tokens, length);
            var last = tokens[length - 1].Text;
            if (phrase > 0)
            {
                length -= phrase;
            }
            else if (IsTempo(last) && !trackNumbers.Contains(int.Parse(last, CultureInfo.InvariantCulture)))
            {
                length--;
            }
            else if (!transpositionStripped && IsTransposition(tokens, length))
            {
                // Nur ein Kürzel: In „Skalen Akk C Eb“ ist das C die Tonleiter, das Eb die Stimmung.
                transpositionStripped = true;
                length--;
            }
            else
            {
                break;
            }
        }

        return length == 0 ? tokens.Count : length;
    }

    private static int PhraseLengthEndingAt(IReadOnlyList<NameToken> tokens, int end)
    {
        foreach (var phrase in Phrases)
        {
            var start = end - phrase.Length;
            if (start < 0)
            {
                continue;
            }

            var matches = true;
            for (var i = 0; i < phrase.Length && matches; i++)
            {
                var word = tokens[start + i].Text;
                matches = phrase[i] == "#" ? IsTempo(word) : phrase[i] == word;
            }

            if (matches)
            {
                return phrase.Length;
            }
        }

        return 0;
    }

    private static bool IsTransposition(IReadOnlyList<NameToken> tokens, int end) =>
        Transpositions.Contains(tokens[end - 1].Text) && !(end >= 2 && tokens[end - 2].Text == "in");

    private static bool IsTempo(string word) =>
        word.Length <= 3
        && word.All(char.IsAsciiDigit)
        && int.Parse(word, CultureInfo.InvariantCulture) is >= MinTempo and <= MaxTempo;
}
