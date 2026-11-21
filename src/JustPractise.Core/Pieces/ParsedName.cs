using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace JustPractise.Core.Pieces;

/// <summary>
/// Ein in Nummer, Wörter und Titel zerlegter Dateiname.
/// </summary>
internal sealed partial class ParsedName
{
    private const string Apostrophes = "'’‘´`";
    private const string EdgePunctuation = ".,;:!?";

    private readonly string _name;
    private readonly int _titleLength;

    private ParsedName(string fileName, string name, string number, List<NameToken> tokens)
    {
        FileName = fileName;
        _name = name;
        Number = number;
        Tokens = tokens;
        _titleLength = Suffixes.TitleLength(tokens, TrackNumbers(number));
        Key = tokens.Count == 0 ? name.ToLowerInvariant() : Join(tokens, _titleLength);
        FullKey = tokens.Count == 0 ? Key : Join(tokens, tokens.Count);
    }

    public string FileName { get; }

    /// <summary>Die abgetrennte Nummer am Anfang, normalisiert, leer ohne Nummer.</summary>
    public string Number { get; }

    public IReadOnlyList<NameToken> Tokens { get; }

    /// <summary>Der erkannte Titel ohne Nummer und Zusätze, normalisiert. Danach wird gruppiert.</summary>
    public string Key { get; }

    /// <summary>Alle Wörter ohne Nummer, normalisiert, also mit Zusätzen.</summary>
    public string FullKey { get; }

    /// <summary>Der Name hat keine Zusätze.</summary>
    public bool IsPlain => _titleLength == Tokens.Count;

    /// <summary>Der Titel für die Anzeige: der Originalname mit Nummer bis zum Ende des erkannten Titels.</summary>
    public string Title
    {
        get
        {
            if (Tokens.Count == 0)
            {
                return _name;
            }

            // Eine im Titel geöffnete Klammer wird mit geschlossen, z. B. „Walzer (Dornröschen)“.
            var end = Tokens[_titleLength - 1].End;
            var open = _name[..end].Count(c => c is '(' or '[') - _name[..end].Count(c => c is ')' or ']');
            while (open > 0 && end < _name.Length && _name[end] is ')' or ']')
            {
                end++;
                open--;
            }

            return _name[..end];
        }
    }

    /// <summary>Die Titel vor jedem harten Trennzeichen innerhalb des Titels, der längste zuerst.</summary>
    public IEnumerable<string> PrefixKeys()
    {
        for (var i = _titleLength - 1; i > 0; i--)
        {
            if (Tokens[i].HardBefore)
            {
                yield return Join(Tokens, i);
            }
        }
    }

    public static ParsedName Parse(string fileName)
    {
        var name = Path.GetFileNameWithoutExtension(fileName).Normalize(NormalizationForm.FormC);

        var bodyStart = 0;
        while (LeadingNumber().Match(name, bodyStart, name.Length - bodyStart) is { Success: true } match
            && match.Index + match.Length < name.Length)
        {
            bodyStart = match.Index + match.Length;
        }

        var number = name[..bodyStart].Trim().ToLowerInvariant();
        return new ParsedName(fileName, name, number, Tokenize(name, bodyStart));
    }

    private static List<NameToken> Tokenize(string name, int start)
    {
        var tokens = new List<NameToken>();
        var hard = false;
        var i = start;
        while (i < name.Length)
        {
            if (char.IsWhiteSpace(name[i]))
            {
                i++;
                continue;
            }

            if (IsHardSeparator(name[i]))
            {
                hard = true;
                i++;
                continue;
            }

            var wordStart = i;
            while (i < name.Length && !char.IsWhiteSpace(name[i]) && !IsHardSeparator(name[i]))
            {
                i++;
            }

            var word = name[wordStart..i];
            if (word is "-" or "–" or "—")
            {
                hard = true;
                continue;
            }

            if (AddWord(tokens, word, wordStart, hard))
            {
                hard = false;
            }
        }

        return tokens;
    }

    private static bool AddWord(List<NameToken> tokens, string word, int start, bool hardBefore)
    {
        var trimmed = word.TrimEnd(EdgePunctuation.ToCharArray());
        var end = start + trimmed.Length;
        var text = string.Concat(trimmed.Where(c => !Apostrophes.Contains(c)))
            .TrimStart(EdgePunctuation.ToCharArray())
            .ToLowerInvariant();
        if (text.Length == 0)
        {
            return false;
        }

        // Angeklebte Zahlen trennen, z. B. „Bossa100“, „60bpm“, „120T“.
        if (GluedNumber().Match(trimmed) is { Success: true } glued && glued.Length == trimmed.Length)
        {
            var first = glued.Groups[1].Success ? glued.Groups[1].Length : glued.Groups[2].Length;
            tokens.Add(new NameToken(text[..first], start + first, hardBefore));
            tokens.Add(new NameToken(text[first..], end, false));
            return true;
        }

        tokens.Add(new NameToken(text, end, hardBefore));
        return true;
    }

    private static HashSet<int> TrackNumbers(string number) =>
        [.. Digits().Matches(number).Select(match => int.Parse(match.Value, CultureInfo.InvariantCulture))];

    private static bool IsHardSeparator(char c) => c is '_' or '(' or ')' or '[' or ']';

    private static string Join(IReadOnlyList<NameToken> tokens, int count) =>
        string.Join(' ', tokens.Take(count).Select(t => t.Text));

    [GeneratedRegex(@"^(?:(?:track|titel)\s*\d+|\d+(?:-\d+)?\.?)[\s_\-]+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex LeadingNumber();

    [GeneratedRegex(@"\d{1,6}", RegexOptions.CultureInvariant)]
    private static partial Regex Digits();

    [GeneratedRegex(@"^(?:(\p{L}+)\d+|(\d+)\p{L}+)$", RegexOptions.CultureInvariant)]
    private static partial Regex GluedNumber();
}
