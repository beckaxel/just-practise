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

    public TunerSettings Tuner { get; init; } = TunerSettings.Default;

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Unknown { get; init; }
}

public sealed record LanguageSettings : IDataSection
{
    public static LanguageSettings Default { get; } = new();

    /// <summary>Sprache der Oberfläche, z. B. <c>de</c>. <c>null</c> folgt dem System.</summary>
    public string? Ui { get; init; }

    /// <summary>
    /// Vorzeichen der Tonnamen: <c>flat</c> für b (Es, B♭); sonst Kreuz (Dis, A♯), wie in der MIDI-Spezifikation.
    /// </summary>
    public string? Accidentals { get; init; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Unknown { get; init; }

    [JsonIgnore]
    public Tuner.Accidentals EffectiveAccidentals => Accidentals == "flat" ? Tuner.Accidentals.Flat : Tuner.Accidentals.Sharp;

    [JsonIgnore]
    public bool IsDefault => Ui is null && Accidentals is null && Unknown is not { Count: > 0 };

    public LanguageSettings WithAccidentals(Tuner.Accidentals accidentals) =>
        this with { Accidentals = accidentals == Tuner.Accidentals.Flat ? "flat" : null };
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

/// <summary>Das Stimmgerät: Kammerton und Stimmung des Instruments.</summary>
public sealed record TunerSettings : IDataSection
{
    public const double DefaultReferenceHz = 440;
    public const double MinReferenceHz = 415;
    public const double MaxReferenceHz = 466;

    public static TunerSettings Default { get; } = new();

    /// <summary>Kammerton (A4) in Hz, 415 bis 466; ohne Angabe 440.</summary>
    public double? ReferenceHz { get; init; }

    /// <summary>
    /// Stimmung als Halbtöne, die der notierte Ton über dem klingenden liegt: 2 für B, 9 für Es, 7 für F, 3 für A;
    /// ohne Angabe 0 (C, klingend).
    /// </summary>
    public int? TransposeSemitones { get; init; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Unknown { get; init; }

    [JsonIgnore]
    public double EffectiveReferenceHz => Math.Clamp(ReferenceHz ?? DefaultReferenceHz, MinReferenceHz, MaxReferenceHz);

    [JsonIgnore]
    public int EffectiveTransposeSemitones => Math.Clamp(TransposeSemitones ?? 0, -24, 24);

    [JsonIgnore]
    public bool IsDefault => ReferenceHz is null && TransposeSemitones is null && Unknown is not { Count: > 0 };

    public TunerSettings WithReferenceHz(double hz)
    {
        var clamped = Math.Clamp(Math.Round(hz, 1), MinReferenceHz, MaxReferenceHz);
        return this with { ReferenceHz = clamped == DefaultReferenceHz ? null : clamped };
    }

    public TunerSettings WithTransposeSemitones(int semitones) =>
        this with { TransposeSemitones = semitones == 0 ? null : semitones };
}
