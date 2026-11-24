using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace JustPractise.Storage;

/// <summary>
/// Meldet das AppImage unter Linux beim Desktop an: eine <c>.desktop</c>-Datei und das Icon im Datenordner des Nutzers.
/// Erst damit kann z. B. KDE das Fenster der App zuordnen und sie in der Taskleiste anpinnen; ohne die Datei merkt es
/// sich den Pfad im Einhängeordner des AppImage, der bei jedem Start anders heißt. Außerdem steht die App im Startmenü.
/// </summary>
/// <remarks>
/// Läuft bei jedem Start und schreibt nur, was sich geändert hat (anderer Pfad des AppImage, neues Icon).
/// Der Name des Icons enthält den Anfang seines SHA-256: Ein neues Icon bekommt einen neuen Namen, so zeigt der
/// Desktop nie ein altes Icon aus seinem Cache. Alte Icons werden gelöscht.
/// </remarks>
public sealed partial class DesktopEntry(string dataHome)
{
    /// <summary>Name der <c>.desktop</c>-Datei; muss zur <c>WM_CLASS</c> des Fensters passen.</summary>
    public const string Id = "just-practise";

    /// <summary>
    /// Der Datenordner des Nutzers: <c>$XDG_DATA_HOME</c>, sonst <c>~/.local/share</c>.
    /// </summary>
    public static DesktopEntry System { get; } = new(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData, Environment.SpecialFolderOption.DoNotVerify));

    public string EntryPath => Path.Combine(dataHome, "applications", Id + ".desktop");

    public string IconDirectory => Path.Combine(dataHome, "icons", "hicolor", "512x512", "apps");

    /// <summary>Schreibt Eintrag und Icon, falls nötig. Gibt <c>true</c> zurück, wenn etwas geschrieben wurde.</summary>
    /// <param name="appImagePath">Der Pfad des AppImage, aus <c>$APPIMAGE</c>.</param>
    /// <param name="icon">Das Icon als PNG mit 512 × 512 Pixeln.</param>
    public bool Update(string appImagePath, ReadOnlySpan<byte> icon)
    {
        ArgumentException.ThrowIfNullOrEmpty(appImagePath);
        var iconName = $"{Id}-{Convert.ToHexStringLower(SHA256.HashData(icon))[..8]}";
        var iconPath = Path.Combine(IconDirectory, iconName + ".png");

        var changed = false;
        if (!File.Exists(iconPath))
        {
            WriteAtomically(iconPath, icon);
            changed = true;
        }

        foreach (var old in Directory.EnumerateFiles(IconDirectory, Id + "-*.png"))
        {
            if (OldIconName().IsMatch(Path.GetFileName(old)) && !string.Equals(old, iconPath, StringComparison.Ordinal))
            {
                File.Delete(old);
            }
        }

        var content = Encoding.UTF8.GetBytes(Content(appImagePath, iconName));
        if (!File.Exists(EntryPath) || !File.ReadAllBytes(EntryPath).AsSpan().SequenceEqual(content))
        {
            WriteAtomically(EntryPath, content);
            changed = true;
        }

        return changed;
    }

    /// <summary>Der Inhalt der <c>.desktop</c>-Datei nach der Desktop Entry Specification.</summary>
    public static string Content(string appImagePath, string iconName) =>
        $"""
        [Desktop Entry]
        Type=Application
        Name=just practise
        Comment=Practise with your own recordings
        Comment[de]=Mit eigenen Aufnahmen üben
        Exec={EscapeString(QuoteArgument(appImagePath))}
        TryExec={EscapeString(appImagePath)}
        Icon={iconName}
        StartupWMClass={Id}
        Terminal=false
        Categories=AudioVideo;Audio;Music;

        """.ReplaceLineEndings("\n");

    /// <summary>
    /// Ein Argument für <c>Exec</c>: in Anführungszeichen, darin <c>" ` $ \</c> mit Backslash; <c>%</c> wird verdoppelt.
    /// </summary>
    private static string QuoteArgument(string value)
    {
        var builder = new StringBuilder("\"");
        foreach (var c in value)
        {
            if (c is '"' or '`' or '$' or '\\')
            {
                builder.Append('\\');
            }

            builder.Append(c == '%' ? "%%" : c.ToString());
        }

        return builder.Append('"').ToString();
    }

    /// <summary>Escapes für Werte vom Typ string: Backslash, Zeilenumbrüche und Tabulatoren.</summary>
    private static string EscapeString(string value) => value
        .Replace("\\", @"\\", StringComparison.Ordinal)
        .Replace("\n", @"\n", StringComparison.Ordinal)
        .Replace("\r", @"\r", StringComparison.Ordinal)
        .Replace("\t", @"\t", StringComparison.Ordinal);

    private static void WriteAtomically(string path, ReadOnlySpan<byte> content)
    {
        var directory = Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(directory);
        var temporary = Path.Combine(directory, $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
        try
        {
            File.WriteAllBytes(temporary, content);
            File.Move(temporary, path, overwrite: true);
        }
        finally
        {
            File.Delete(temporary);
        }
    }

    [GeneratedRegex("^just-practise-[0-9a-f]{8}\\.png$", RegexOptions.CultureInvariant)]
    private static partial Regex OldIconName();
}
