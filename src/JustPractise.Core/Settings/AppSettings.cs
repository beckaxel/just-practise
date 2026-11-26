using System.Text.Json;
using System.Text.Json.Serialization;
using JustPractise.Core.DataFiles;
using JustPractise.Core.Metronome;

namespace JustPractise.Core.Settings;

/// <summary>Inhalt von <c>settings.json</c>: alles, was der Nutzer einstellt.</summary>
public sealed record AppSettings
{
    public const int CurrentFormat = 1;

    [JsonPropertyOrder(-1)]
    public int Format { get; init; } = CurrentFormat;

    public LanguageSettings Language { get; init; } = LanguageSettings.Default;

    public MetronomeSettings Metronome { get; init; } = MetronomeSettings.Default;

    public TimerSettings Timer { get; init; } = TimerSettings.Default;

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Unknown { get; init; }
}

public sealed record LanguageSettings : IDataSection
{
    public static LanguageSettings Default { get; } = new();

    /// <summary>Sprache der Oberfläche, z. B. <c>de</c>. <c>null</c> folgt dem System.</summary>
    public string? Ui { get; init; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Unknown { get; init; }

    [JsonIgnore]
    public bool IsDefault => Ui is null && Unknown is not { Count: > 0 };
}

public sealed record MetronomeSettings : IDataSection
{
    public static MetronomeSettings Default { get; } = new();

    /// <summary>Die zuletzt verwendete Einstellung.</summary>
    public MetronomeSetup Current { get; init; } = MetronomeSetup.Default;

    /// <summary>Lautstärke des Metronoms in dB, unabhängig vom Player. Gehört nicht zu einer Einstellung.</summary>
    public double? VolumeDb { get; init; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Unknown { get; init; }

    [JsonIgnore]
    public double EffectiveVolumeDb => VolumeDb ?? 0.0;

    [JsonIgnore]
    public bool IsDefault => Current.IsDefault && VolumeDb is null && Unknown is not { Count: > 0 };

    public MetronomeSettings WithVolumeDb(double volumeDb)
    {
        var rounded = Math.Round(volumeDb, 1);
        return this with { VolumeDb = rounded == 0.0 ? null : rounded };
    }
}

/// <summary>Eine Metronom-Einstellung: Tempo und Taktart. Später auch für benannte Einstellungen.</summary>
public sealed record MetronomeSetup : IDataSection
{
    public const int DefaultBpm = 120;
    public const int MinBpm = BeatClock.MinBpm;
    public const int MaxBpm = BeatClock.MaxBpm;

    public static MetronomeSetup Default { get; } = new();

    public int? Bpm { get; init; }

    /// <summary>Taktart als Text wie <c>4/4</c>. Bleibt als Text, damit eine unbekannte Angabe erhalten bleibt.</summary>
    public string? Meter { get; init; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Unknown { get; init; }

    [JsonIgnore]
    public int EffectiveBpm => Math.Clamp(Bpm ?? DefaultBpm, MinBpm, MaxBpm);

    [JsonIgnore]
    public Meter EffectiveMeter =>
        Metronome.Meter.TryParse(Meter, out var meter) ? meter : Metronome.Meter.FourFour;

    [JsonIgnore]
    public bool IsDefault => Bpm is null && Meter is null && Unknown is not { Count: > 0 };

    public MetronomeSetup WithBpm(int bpm)
    {
        var clamped = Math.Clamp(bpm, MinBpm, MaxBpm);
        return this with { Bpm = clamped == DefaultBpm ? null : clamped };
    }

    public MetronomeSetup WithMeter(Meter meter) =>
        this with { Meter = meter == Metronome.Meter.FourFour ? null : meter.ToString() };
}

/// <summary>Der Übetimer: die zuletzt eingestellte Dauer in ganzen Minuten.</summary>
public sealed record TimerSettings : IDataSection
{
    public const int DefaultMinutes = 10;
    public const int MinMinutes = 1;
    public const int MaxMinutes = 60;

    public static TimerSettings Default { get; } = new();

    public int? Minutes { get; init; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Unknown { get; init; }

    [JsonIgnore]
    public int EffectiveMinutes => Math.Clamp(Minutes ?? DefaultMinutes, MinMinutes, MaxMinutes);

    [JsonIgnore]
    public bool IsDefault => Minutes is null && Unknown is not { Count: > 0 };

    public TimerSettings WithMinutes(int minutes)
    {
        var clamped = Math.Clamp(minutes, MinMinutes, MaxMinutes);
        return this with { Minutes = clamped == DefaultMinutes ? null : clamped };
    }
}
