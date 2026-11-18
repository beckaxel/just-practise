using System.Diagnostics;

namespace JustPractise.Audio.Engine;

/// <summary>
/// Zählt verspätete Audio-Callbacks (Prüfpunkt 1). Ein Abstand von deutlich mehr als einer Pufferlänge heißt,
/// dass der Callback zu spät kam, etwa weil eine Speicherbereinigung den Thread angehalten hat.
/// </summary>
/// <remarks>Läuft auf dem Audio-Thread: keine Allokation, keine Sperre.</remarks>
public sealed class CallbackMonitor
{
    /// <summary>Ab dem 1,8-Fachen der erwarteten Zeit gilt ein Callback als verspätet (wie in Prüfpunkt 1).</summary>
    private const double LateFactor = 1.8;

    private long _last;
    private long _late;
    private long _maxGapTicks;

    public long LateCallbacks => Volatile.Read(ref _late);

    public TimeSpan MaxGap => TimeSpan.FromSeconds((double)Volatile.Read(ref _maxGapTicks) / Stopwatch.Frequency);

    /// <summary>Meldet einen Callback über <paramref name="frames"/> Frames zum Zeitpunkt <paramref name="timestamp"/> (Stopwatch-Ticks).</summary>
    public void Tick(long timestamp, int frames, int sampleRate)
    {
        if (_last != 0)
        {
            var gap = timestamp - _last;
            if (gap > _maxGapTicks)
            {
                Volatile.Write(ref _maxGapTicks, gap);
            }

            if (gap > (double)frames / sampleRate * Stopwatch.Frequency * LateFactor)
            {
                Interlocked.Increment(ref _late);
            }
        }

        _last = timestamp;
    }

    /// <summary>Beginnt neu, z. B. nachdem das Gerät neu geöffnet wurde; die Lücke dazwischen zählt nicht.</summary>
    public void Restart() => _last = 0;
}
