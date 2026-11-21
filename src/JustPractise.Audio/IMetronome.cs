using JustPractise.Audio.Graph;
using JustPractise.Core.Metronome;

namespace JustPractise.Audio;

/// <summary>Das Metronom, wie es die Oberfläche sieht. Alle Aufrufe vom UI-Thread.</summary>
public interface IMetronome
{
    bool IsRunning { get; }

    int Bpm { get; }

    Meter Meter { get; }

    /// <summary>Der zuletzt berechnete Schlag für die Schlaganzeige, <c>null</c> wenn das Metronom steht.</summary>
    BeatInfo? LastBeat { get; }

    /// <summary>Startet (immer mit der Eins) oder hält an.</summary>
    void SetRunning(bool running);

    void SetTempo(int bpm);

    void SetMeter(Meter meter);

    void SetVolume(double volumeDb);
}
