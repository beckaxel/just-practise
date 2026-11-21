using System.Text.Json;
using System.Text.Json.Serialization;

namespace JustPractise.Core.Pieces;

/// <summary>Inhalt der Sidecar-Datei eines Stücks (<c>&lt;Titel&gt;.jps</c>).</summary>
public sealed record SidecarFile
{
    public const int CurrentFormat = 1;
    public const string Extension = ".jps";

    [JsonPropertyOrder(-1)]
    public int Format { get; init; } = CurrentFormat;

    /// <summary>Einstellungen pro Audiodatei, über den Dateinamen ohne Pfad. Verwaiste Einträge bleiben erhalten.</summary>
    public IReadOnlyDictionary<string, FileSettings> Files { get; init; } = new Dictionary<string, FileSettings>();

    /// <summary>Nur bei von Hand korrigierter Gruppierung: die Dateien des Stücks.</summary>
    public IReadOnlyList<string>? Members { get; init; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Unknown { get; init; }

    public static string FileNameFor(string pieceTitle) => pieceTitle + Extension;

    public FileSettings SettingsFor(string fileName) =>
        Files.TryGetValue(fileName, out var settings) ? settings : FileSettings.Default;

    /// <summary>Setzt die Einstellungen einer Datei. Stehen sie auf Standard, fällt der Eintrag weg.</summary>
    public SidecarFile WithSettings(string fileName, FileSettings settings)
    {
        var files = new Dictionary<string, FileSettings>(Files, StringComparer.Ordinal);
        if (settings.IsDefault)
        {
            files.Remove(fileName);
        }
        else
        {
            files[fileName] = settings;
        }

        return this with { Files = files };
    }
}
