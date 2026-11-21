namespace JustPractise.App.FileBrowser;

/// <summary>Vergleicht und zerlegt Ordnerpfade so, wie das Dateisystem des Betriebssystems sie versteht.</summary>
public static class FolderPaths
{
    /// <summary>Unter Windows und macOS zählt Groß- und Kleinschreibung in Pfaden nicht, unter Linux schon.</summary>
    public static StringComparison Comparison { get; } =
        OperatingSystem.IsWindows() || OperatingSystem.IsMacOS() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    /// <summary>Der absolute Pfad ohne Trenner am Ende (außer bei Wurzeln wie <c>/</c> oder <c>C:\</c>).</summary>
    public static string Normalize(string path) => Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));

    public static bool AreSame(string a, string b) => string.Equals(Normalize(a), Normalize(b), Comparison);

    /// <summary>Der Musikordner, in dem <paramref name="path"/> liegt (oder der er ist); bei mehreren der tiefste.</summary>
    public static string? FindRoot(IEnumerable<string> roots, string path)
    {
        var normalized = Normalize(path);
        return roots
            .Where(root => IsInside(Normalize(root), normalized))
            .OrderByDescending(root => Normalize(root).Length)
            .FirstOrDefault();
    }

    /// <summary>Die Ordnernamen von <paramref name="root"/> bis <paramref name="path"/>, ohne den Musikordner selbst.</summary>
    public static IReadOnlyList<string> Segments(string root, string path)
    {
        var relative = Path.GetRelativePath(Normalize(root), Normalize(path));
        return relative == "."
            ? []
            : relative.Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries);
    }

    /// <summary>Der angezeigte Name eines Ordners; bei einer Wurzel wie <c>/</c> oder <c>D:\</c> der Pfad selbst.</summary>
    public static string DisplayName(string path)
    {
        var normalized = Normalize(path);
        var name = Path.GetFileName(normalized);
        return string.IsNullOrEmpty(name) ? normalized : name;
    }

    private static bool IsInside(string root, string path)
    {
        if (string.Equals(root, path, Comparison))
        {
            return true;
        }

        var prefix = Path.EndsInDirectorySeparator(root) ? root : root + Path.DirectorySeparatorChar;
        return path.StartsWith(prefix, Comparison);
    }
}
