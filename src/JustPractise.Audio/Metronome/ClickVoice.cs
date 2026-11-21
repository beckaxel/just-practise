using JustPractise.Core.Metronome;

namespace JustPractise.Audio.Metronome;

/// <summary>
/// Spielt die Klicks des Metronoms sample-genau in den Ausgabepuffer.
/// </summary>
/// <remarks>Läuft auf dem Audio-Thread: keine Allokation, keine Sperre.</remarks>
public sealed class ClickVoice(BeatClock clock, ClickSound sound)
{
    private ReadOnlyMemory<float> _playing;
    private int _playingPosition;
    private float _playingGain;
    private float _gain = 1f;

    public BeatClock Clock { get; } = clock;

    public bool IsRunning => Clock.IsRunning;

    public void SetVolume(double volumeDb) => _gain = Decibels.ToGain(volumeDb);

    public void Stop()
    {
        Clock.Stop();
        _playing = ReadOnlyMemory<float>.Empty;
    }

    /// <summary>
    /// Mischt die Klicks dieses Blocks in <paramref name="buffer"/> (interleaved) und liefert den letzten
    /// Schlag, der in diesem Block begann.
    /// </summary>
    public Beat? Render(Span<float> buffer, int channels, long blockStart)
    {
        var frames = buffer.Length / channels;
        var blockEnd = blockStart + frames;
        Beat? lastBeat = null;
        var position = 0;

        while (Clock.TryNextBeat(blockEnd, out var beat))
        {
            var offset = (int)Math.Max(0, beat.Frame - blockStart);
            Mix(buffer, channels, position, offset);
            _playing = beat.Accented ? sound.Accented : sound.Normal;
            _playingPosition = 0;
            _playingGain = _gain;
            position = offset;
            lastBeat = beat;
        }

        Mix(buffer, channels, position, frames);
        return lastBeat;
    }

    /// <summary>Mischt den laufenden Klick in die Frames von <paramref name="from"/> bis vor <paramref name="to"/>.</summary>
    private void Mix(Span<float> buffer, int channels, int from, int to)
    {
        var samples = _playing.Span;
        var count = Math.Min(to - from, samples.Length - _playingPosition);
        for (var i = 0; i < count; i++)
        {
            var sample = samples[_playingPosition + i] * _playingGain;
            var frame = (from + i) * channels;
            for (var c = 0; c < channels; c++)
            {
                buffer[frame + c] += sample;
            }
        }

        _playingPosition += Math.Max(0, count);
    }
}
