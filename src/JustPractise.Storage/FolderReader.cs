using JustPractise.Core.Pieces;

namespace JustPractise.Storage;

/// <summary>Der Inhalt eines Ordners im Dateibrowser.</summary>
/// <param name="Path">der Ordner</param>
/// <param name="IsReachable"><c>false</c>, wenn der Ordner fehlt oder nicht lesbar ist</param>
/// <param name="Subfolders">die Namen der Unterordner, sortiert</param>
/// <param name="Pieces">die Stücke aus den Audiodateien, nach Titel sortiert</param>
public sealed record FolderContent(string Path, bool IsReachable, IReadOnlyList<string> Subfolders, IReadOnlyList<Piece> Pieces);

/// <summary>Liest einen Ordner für den Dateibrowser ein.</summary>
/// <remarks>Blockiert, z. B. bei Cloud-Ordnern. Nicht vom UI-Thread aus aufrufen.</remarks>
public static class FolderReader
{
    private static readonly EnumerationOptions Options = new()
    {
        AttributesToSkip = FileAttributes.Hidden | FileAttributes.System,
        IgnoreInaccessible = true,
        RecurseSubdirectories = false,
    };

    public static FolderContent Read(string path)
    {
        try
        {
            var directory = new DirectoryInfo(path);
            var options = Options;

            var subfolders = directory.EnumerateDirectories("*", options)
                .Select(folder => folder.Name)
                .Where(name => !IsHidden(name))
                .Order(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var audioFiles = directory.EnumerateFiles("*", options)
                .Select(file => file.Name)
                .Where(name => !IsHidden(name) && AudioFiles.IsSupported(name));

            return new FolderContent(path, true, subfolders, PieceGrouper.Group(audioFiles));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            return new FolderContent(path, false, [], []);
        }
    }

    /// <summary>
    /// Ob der Ordner etwas zum Anzeigen enthält: einen Unterordner oder eine Audiodatei. Hört beim ersten Treffer auf.
    /// </summary>
    public static bool HasEntries(string path)
    {
        try
        {
            var directory = new DirectoryInfo(path);
            return directory.EnumerateDirectories("*", Options).Any(folder => !IsHidden(folder.Name))
                || directory.EnumerateFiles("*", Options).Any(file => !IsHidden(file.Name) && AudioFiles.IsSupported(file.Name));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            return false;
        }
    }

    // Unter Linux und macOS gelten Namen mit Punkt am Anfang als versteckt (z. B. „.mscbackup“).
    private static bool IsHidden(string name) => name.StartsWith('.');
}
