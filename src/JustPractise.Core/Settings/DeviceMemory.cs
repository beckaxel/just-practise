using System.Text.Json;
using System.Text.Json.Serialization;
using JustPractise.Core.Placement;

namespace JustPractise.Core.Settings;

/// <summary>Inhalt von <c>device.json</c>: was nur auf diesem Rechner Sinn ergibt. Wird nie exportiert.</summary>
public sealed record DeviceMemory
{
    public const int CurrentFormat = 1;

    [JsonPropertyOrder(-1)]
    public int Format { get; init; } = CurrentFormat;

    /// <summary>Die gewählten Musikordner als absolute Pfade.</summary>
    public IReadOnlyList<string> MusicFolders { get; init; } = [];

    /// <summary>Der zuletzt geöffnete Ordner als absoluter Pfad.</summary>
    public string? LastFolder { get; init; }

    /// <summary>Lage des Hauptfensters beim letzten Beenden; <c>null</c> vor dem ersten Beenden.</summary>
    public WindowPlacement? Window { get; init; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Unknown { get; init; }
}
