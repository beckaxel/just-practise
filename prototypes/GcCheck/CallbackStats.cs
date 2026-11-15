using System.Diagnostics;
using SoundFlow.Enums;

namespace JustPractise.Prototypes.GcCheck;

/// <summary>
/// Misst die Abstände der Audio-Callbacks. Ein Abstand weit über der Puffergröße heißt, dass der
/// Callback zu spät kam, etwa weil ein GC den Thread beim Eintritt in verwalteten Code angehalten hat.
/// Läuft auf dem Audio-Thread: keine Allokationen, keine Locks.
/// </summary>
internal sealed class CallbackStats(int channels, int sampleRate)
{
    private long _last;
    private long _count;
    private long _late;
    private long _maxTicks;
    private long _intervalMaxTicks;
    private int _frames;

    public long Count => _count;

    public long Late => Volatile.Read(ref _late);

    public double MaxGapMilliseconds => ToMilliseconds(_maxTicks);

    public double BufferMilliseconds => _frames * 1000.0 / sampleRate;

    public void OnAudioProcessed(Span<float> samples, Capability capability)
    {
        var now = Stopwatch.GetTimestamp();
        _frames = samples.Length / channels;

        if (_last != 0)
        {
            var gap = now - _last;
            var expected = (double)_frames / sampleRate * Stopwatch.Frequency;
            _count++;
            _maxTicks = Math.Max(_maxTicks, gap);
            if (gap > _intervalMaxTicks)
            {
                Volatile.Write(ref _intervalMaxTicks, gap);
            }

            if (gap > expected * 1.8)
            {
                Interlocked.Increment(ref _late);
            }
        }

        _last = now;
    }

    /// <summary>Größter Abstand seit dem letzten Aufruf (für das Protokoll).</summary>
    public double TakeIntervalMaxMilliseconds() => ToMilliseconds(Interlocked.Exchange(ref _intervalMaxTicks, 0));

    public void Reset()
    {
        _last = 0;
        _count = 0;
        _late = 0;
        _maxTicks = 0;
    }

    private static double ToMilliseconds(long ticks) => ticks * 1000.0 / Stopwatch.Frequency;
}
