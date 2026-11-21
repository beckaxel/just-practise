namespace JustPractise.Core.Pieces;

/// <summary>
/// Fasst die Dateien eines Ordners zu Stücken zusammen. Die Regeln stehen in
/// <c>docs/funktionen.md</c> unter „Stücke und Gruppierung“.
/// </summary>
public static class PieceGrouper
{
    /// <summary>Gruppiert Dateinamen (ohne Pfad) eines Ordners. Die Stücke sind nach Titel sortiert.</summary>
    public static IReadOnlyList<Piece> Group(IEnumerable<string> fileNames)
    {
        var groups = fileNames
            .Select(ParsedName.Parse)
            .GroupBy(name => name.Key, StringComparer.Ordinal)
            .SelectMany(SplitByNumber)
            .ToList();

        MergeAtHardSeparators(groups);

        return [.. groups.Select(ToPiece).OrderBy(piece => piece.Title, StringComparer.OrdinalIgnoreCase)];
    }

    /// <summary>
    /// Gleiche Namen mit verschiedenen Nummern sind verschiedene Stücke. Kommt das vor,
    /// wird nach Nummer getrennt, sonst gehören Dateien mit gleichem Titel zusammen.
    /// </summary>
    private static IEnumerable<List<ParsedName>> SplitByNumber(IEnumerable<ParsedName> sameKey)
    {
        var names = sameKey.ToList();
        var ambiguous = names
            .GroupBy(name => name.FullKey, StringComparer.Ordinal)
            .Any(same => same.Select(name => name.Number).Distinct().Skip(1).Any());

        return ambiguous
            ? names.GroupBy(name => name.Number, StringComparer.Ordinal).Select(byNumber => byNumber.ToList())
            : [names];
    }

    /// <summary>
    /// Ein Stück, dessen Titel nach einem harten Trennzeichen weitergeht, kommt zu dem Stück, dessen
    /// Titel genau dem Teil davor entspricht, sofern es dazu eine Datei ohne Zusätze gibt.
    /// </summary>
    private static void MergeAtHardSeparators(List<List<ParsedName>> groups)
    {
        bool merged;
        do
        {
            merged = false;
            var byKey = groups.ToLookup(group => group[0].Key, StringComparer.Ordinal);
            foreach (var group in groups)
            {
                var target = group
                    .SelectMany(name => name.PrefixKeys())
                    .Distinct(StringComparer.Ordinal)
                    .OrderByDescending(key => key.Length)
                    .Select(key => byKey[key].ToList())
                    .FirstOrDefault(candidates =>
                        candidates.Count == 1
                        && !ReferenceEquals(candidates[0], group)
                        && candidates[0].Any(name => name.IsPlain))?[0];

                if (target is not null)
                {
                    target.AddRange(group);
                    groups.Remove(group);
                    merged = true;
                    break;
                }
            }
        }
        while (merged);
    }

    private static Piece ToPiece(List<ParsedName> group)
    {
        var main = group
            .OrderBy(name => name.IsPlain ? 0 : 1)
            .ThenBy(name => name.Key.Length)
            .ThenBy(name => name.FileName, StringComparer.OrdinalIgnoreCase)
            .First();

        return new Piece(main.Title, [.. group.Select(name => name.FileName).Order(StringComparer.OrdinalIgnoreCase)]);
    }
}
