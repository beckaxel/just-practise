using JustPractise.Core.Metronome;

namespace JustPractise.Audio.Graph;

public enum AudioCommandKind
{
    None,
    MetronomeStart,
    MetronomeStop,
    MetronomeTempo,
    MetronomeMeter,
    MetronomeVolume,
}

/// <summary>Ein Befehl an den Audio-Thread. Ein Struct, damit beim Einstellen und Abholen kein Speicher entsteht.</summary>
public readonly record struct AudioCommand(AudioCommandKind Kind, int Number = 0, int Second = 0, double Value = 0)
{
    public static AudioCommand MetronomeStart() => new(AudioCommandKind.MetronomeStart);

    public static AudioCommand MetronomeStop() => new(AudioCommandKind.MetronomeStop);

    public static AudioCommand MetronomeTempo(int bpm) => new(AudioCommandKind.MetronomeTempo, bpm);

    public static AudioCommand MetronomeMeter(Meter meter) => new(AudioCommandKind.MetronomeMeter, meter.Beats, meter.NoteValue);

    public static AudioCommand MetronomeVolume(double volumeDb) => new(AudioCommandKind.MetronomeVolume, Value: volumeDb);
}
