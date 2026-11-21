using JustPractise.Audio.Graph;
using JustPractise.Core.Metronome;

namespace JustPractise.Audio.Metronome;

/// <summary>Das Metronom für die Oberfläche: schickt Befehle an den Audio-Thread.</summary>
public sealed class AudioMetronome(AudioSession session) : IMetronome
{
    public bool IsRunning => session.MetronomeRunning;

    public int Bpm => session.Bpm;

    public Meter Meter => session.Meter;

    public BeatInfo? LastBeat => session.Status.LastBeat;

    public void SetRunning(bool running) => session.SetMetronomeRunning(running);

    public void SetTempo(int bpm) => session.SetMetronomeTempo(bpm);

    public void SetMeter(Meter meter) => session.SetMetronomeMeter(meter);

    public void SetVolume(double volumeDb) => session.SetMetronomeVolume(volumeDb);
}
