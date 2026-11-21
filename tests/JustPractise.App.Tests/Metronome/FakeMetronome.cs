using JustPractise.Audio;
using JustPractise.Audio.Graph;
using JustPractise.Core.Metronome;

namespace JustPractise.App.Tests.Metronome;

/// <summary>Ein Metronom ohne Audio. Den Schlag setzt der Test.</summary>
internal sealed class FakeMetronome : IMetronome
{
    public bool IsRunning { get; private set; }

    public int Bpm { get; private set; }

    public Meter Meter { get; private set; }

    public double VolumeDb { get; private set; }

    public BeatInfo? LastBeat { get; set; }

    public void SetRunning(bool running)
    {
        IsRunning = running;
        if (!running)
        {
            LastBeat = null;
        }
    }

    public void SetTempo(int bpm) => Bpm = bpm;

    public void SetMeter(Meter meter) => Meter = meter;

    public void SetVolume(double volumeDb) => VolumeDb = volumeDb;
}
