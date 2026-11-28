using JustPractise.Core.Waveform;

namespace JustPractise.Audio;

/// <summary>Der Stand des Players für die Oberfläche.</summary>
/// <param name="Path">die geladene Datei, <c>null</c> ohne Datei</param>
/// <param name="IsPlaying">spielt gerade; am Ende hält der Player von selbst an</param>
/// <param name="Position">die gerade hörbare Stelle</param>
/// <param name="Duration">die Länge, soweit schon decodiert</param>
/// <param name="IsComplete">ganz decodiert, <paramref name="Duration"/> ist endgültig</param>
/// <param name="Waveform">die Spitzen der Wellenform, <c>null</c>, bis die Datei ganz decodiert und durchgerechnet ist</param>
public readonly record struct PlayerState(string? Path, bool IsPlaying, TimeSpan Position, TimeSpan Duration, bool IsComplete, WaveformPeaks? Waveform = null)
{
    public bool IsLoaded => Path is not null;

    public bool IsAtEnd => IsComplete && !IsPlaying && Position >= Duration;
}

/// <summary>Der Audioplayer, wie ihn die Oberfläche sieht. Alle Aufrufe vom UI-Thread.</summary>
public interface IPlayer
{
    /// <summary>Wird regelmäßig abgefragt, z. B. 30-mal pro Sekunde.</summary>
    PlayerState State { get; }

    /// <summary>
    /// Lädt eine Datei, angehalten am Anfang. Kehrt zurück, sobald sie abspielbar ist; der Rest wird im Hintergrund
    /// decodiert. Ein neuer Aufruf bricht einen laufenden ab.
    /// </summary>
    /// <exception cref="FileNotFoundException">Die Datei gibt es nicht.</exception>
    /// <exception cref="InvalidDataException">Die Datei ist kein lesbares Audio.</exception>
    Task LoadAsync(string path, CancellationToken cancellationToken = default);

    void Unload();

    void Play();

    void Pause();

    void Seek(TimeSpan position);

    void ToStart();

    /// <summary>Tempo als Faktor, 0,5 bis 1,5.</summary>
    void SetTempo(double tempo);

    void SetVolume(double volumeDb);
}
