using JustPractise.Core.Pieces;

namespace JustPractise.Storage;

/// <summary>
/// Findet und löscht die Sidecar-Dateien (<c>.jps</c>) unter einem Ordner, z. B. wenn ein Musikordner entfernt wird.
/// </summary>
/// <remarks>Blockiert, z. B. bei Cloud-Ordnern. Nicht vom UI-Thread aus aufrufen.</remarks>
public static class SidecarCleanup
{
    /// <summary>
    /// Alle Sidecar-Dateien unter <paramref name="folder"/>, auch in Unterordnern. Ordner in
    /// <paramref name="keep"/> (z. B. andere Musikordner, die darin liegen) werden übersprungen.
    /// </summary>
    public static IReadOnlyList<string> Find(string folder, IReadOnlyCollection<string> keep)
    {
        ArgumentNullException.ThrowIfNull(keep);
        var found = new List<string>();
        var pending = new Stack<string>();
        pending.Push(folder);
        var options = new EnumerationOptions { IgnoreInaccessible = true, RecurseSubdirectories = false, AttributesToSkip = 0 };
        while (pending.Count > 0)
        {
            var current = pending.Pop();
            try
            {
                found.AddRange(Directory.EnumerateFiles(current, "*" + SidecarFile.Extension, options)
                    .Where(path => path.EndsWith(SidecarFile.Extension, StringComparison.OrdinalIgnoreCase)));
                foreach (var sub in Directory.EnumerateDirectories(current, "*", options))
                {
                    if (!keep.Any(other => IsSameOrInside(sub, other)))
                    {
                        pending.Push(sub);
                    }
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Security.SecurityException)
            {
                // Nicht lesbare Ordner überspringen.
            }
        }

        found.Sort(StringComparer.Ordinal);
        return found;
    }

    /// <summary>Löscht die Dateien und gibt zurück, wie viele sich nicht löschen ließen.</summary>
    public static int Delete(IEnumerable<string> files)
    {
        ArgumentNullException.ThrowIfNull(files);
        var failed = 0;
        foreach (var file in files)
        {
            try
            {
                File.Delete(file);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                failed++;
            }
        }

        return failed;
    }

    private static bool IsSameOrInside(string folder, string other)
    {
        var comparison = OperatingSystem.IsWindows() || OperatingSystem.IsMacOS() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        var a = Path.TrimEndingDirectorySeparator(Path.GetFullPath(folder));
        var b = Path.TrimEndingDirectorySeparator(Path.GetFullPath(other));
        return string.Equals(a, b, comparison);
    }
}
