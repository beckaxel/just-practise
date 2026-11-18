using JustPractise.Audio.Player;
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
    PlayerLoad,
    PlayerPlay,
    PlayerPause,
    PlayerSeek,
    PlayerTempo,
    PlayerVolume,
}

/// <summary>
/// Ein Befehl an den Audio-Thread. Ein Struct, damit beim Einstellen und Abholen kein Speicher entsteht.
/// <see cref="Payload"/> trägt nur fertig angelegte Objekte hinein, etwa eine geladene Datei.
/// </summary>
public readonly record struct AudioCommand(AudioCommandKind Kind, long Number = 0, int Second = 0, double Value = 0, object? Payload = null)
{
    public static AudioCommand MetronomeStart() => new(AudioCommandKind.MetronomeStart);

    public static AudioCommand MetronomeStop() => new(AudioCommandKind.MetronomeStop);

    public static AudioCommand MetronomeTempo(int bpm) => new(AudioCommandKind.MetronomeTempo, bpm);

    public static AudioCommand MetronomeMeter(Meter meter) => new(AudioCommandKind.MetronomeMeter, meter.Beats, meter.NoteValue);

    public static AudioCommand MetronomeVolume(double volumeDb) => new(AudioCommandKind.MetronomeVolume, Value: volumeDb);

    /// <summary>Setzt eine Datei ein oder nimmt sie mit <c>null</c> heraus. Die bisherige kommt über die Rückgabe zurück.</summary>
    public static AudioCommand PlayerLoad(LoadedTrack? track) => new(AudioCommandKind.PlayerLoad, Payload: track);

    public static AudioCommand PlayerPlay() => new(AudioCommandKind.PlayerPlay);

    public static AudioCommand PlayerPause() => new(AudioCommandKind.PlayerPause);

    public static AudioCommand PlayerSeek(long frame) => new(AudioCommandKind.PlayerSeek, frame);

    public static AudioCommand PlayerTempo(double tempo) => new(AudioCommandKind.PlayerTempo, Value: tempo);

    public static AudioCommand PlayerVolume(double volumeDb) => new(AudioCommandKind.PlayerVolume, Value: volumeDb);
}

/// <summary>Eine Datei, die der Audio-Thread nicht mehr braucht und die außerhalb freigegeben wird.</summary>
public readonly record struct RetiredTrack(LoadedTrack Track);
