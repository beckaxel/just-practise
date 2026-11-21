using System.Text.Json;
using System.Text.Json.Serialization;
using JustPractise.Core.DataFiles;

namespace JustPractise.Core.Pieces;

/// <summary>Einstellungen einer Audiodatei in der Sidecar-Datei. <c>null</c> heißt Standardwert.</summary>
public sealed record FileSettings : IDataSection
{
    public const double MinTempo = 0.5;
    public const double MaxTempo = 1.5;

    public static FileSettings Default { get; } = new();

    /// <summary>Tempo als Faktor, 0.8 entspricht 80 %.</summary>
    public double? Tempo { get; init; }

    /// <summary>Verstärkung in dB, 0 heißt unverändert.</summary>
    public double? VolumeDb { get; init; }

    public LoopSettings? Loop { get; init; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Unknown { get; init; }

    [JsonIgnore]
    public double EffectiveTempo => Math.Clamp(Tempo ?? 1.0, MinTempo, MaxTempo);

    [JsonIgnore]
    public double EffectiveVolumeDb => VolumeDb ?? 0.0;

    [JsonIgnore]
    public bool IsDefault => Tempo is null && VolumeDb is null && Loop is null && Unknown is not { Count: > 0 };

    /// <summary>Setzt das Tempo, auf ganze Prozent gerundet. 100 % wird nicht gespeichert.</summary>
    public FileSettings WithTempo(double tempo)
    {
        var rounded = Math.Round(Math.Clamp(tempo, MinTempo, MaxTempo), 2);
        return this with { Tempo = rounded == 1.0 ? null : rounded };
    }

    /// <summary>Setzt die Lautstärke, auf 0,1 dB gerundet. 0 dB wird nicht gespeichert.</summary>
    public FileSettings WithVolumeDb(double volumeDb)
    {
        var rounded = Math.Round(volumeDb, 1);
        return this with { VolumeDb = rounded == 0.0 ? null : rounded };
    }
}

/// <summary>A-B-Loop in ganzen Millisekunden.</summary>
public sealed record LoopSettings
{
    public int StartMs { get; init; }

    public int EndMs { get; init; }

    public bool Enabled { get; init; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Unknown { get; init; }
}
