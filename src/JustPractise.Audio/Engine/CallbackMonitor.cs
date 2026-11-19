using System.Diagnostics;

namespace JustPractise.Audio.Engine;

/// <summary>
/// Zählt verspätete Audio-Callbacks (Prüfpunkt 1). Ein Abstand von deutlich mehr als einer Pufferlänge heißt,
/// dass der Callback zu spät kam, etwa weil eine Speicherbereinigung den Thread angehalten hat.
/// </summary>
/// <remarks>
/// <para>
/// Als Pufferlänge gilt die größere von Geräteperiode und Länge des einzelnen Blocks. Rechnet das Gerät die Rate um
/// (unter Windows z. B. 44,1 kHz auf die 48 kHz des Mischers), kommen die Blöcke in wechselnder, teils sehr kleiner
/// Größe, aber weiter im Takt der Periode. Gemessen an einem kleinen Block wäre schon der normale Abstand „verspätet“.
/// </para>
/// <para>Läuft auf dem Audio-Thread: keine Allokation, keine Sperre.</para>
/// </remarks>
public sealed class CallbackMonitor(TimeSpan period)
{
    /// <summary>Ab dem 1,8-Fachen der erwarteten Zeit gilt ein Callback als verspätet (wie in Prüfpunkt 1).</summary>
    private const double LateFactor = 1.8;

    private readonly double _periodTicks = period.TotalSeconds * Stopwatch.Frequency;
    private long _last;
    private long _late;
    private long _maxGapTicks;
    private int _minFrames = int.MaxValue;
    private int _maxFrames;

    public long LateCallbacks => Volatile.Read(ref _late);

    public TimeSpan MaxGap => TimeSpan.FromSeconds((double)Volatile.Read(ref _maxGapTicks) / Stopwatch.Frequency);

    /// <summary>Kleinster Block in Frames seit dem Öffnen des Geräts, 0 vor dem ersten Callback.</summary>
    public int MinFrames => Volatile.Read(ref _maxFrames) == 0 ? 0 : Volatile.Read(ref _minFrames);

    /// <summary>Größter Block in Frames seit dem Öffnen des Geräts.</summary>
    public int MaxFrames => Volatile.Read(ref _maxFrames);

    /// <summary>Meldet einen Callback über <paramref name="frames"/> Frames zum Zeitpunkt <paramref name="timestamp"/> (Stopwatch-Ticks).</summary>
    public void Tick(long timestamp, int frames, int sampleRate)
    {
        if (frames < _minFrames)
        {
            Volatile.Write(ref _minFrames, frames);
        }

        if (frames > _maxFrames)
        {
            Volatile.Write(ref _maxFrames, frames);
        }

        if (_last != 0)
        {
            var gap = timestamp - _last;
            if (gap > _maxGapTicks)
            {
                Volatile.Write(ref _maxGapTicks, gap);
            }

            var expected = Math.Max(_periodTicks, (double)frames / sampleRate * Stopwatch.Frequency);
            if (gap > expected * LateFactor)
            {
                Interlocked.Increment(ref _late);
            }
        }

        _last = timestamp;
    }

    /// <summary>
    /// Beginnt neu, z. B. nachdem das Gerät neu geöffnet wurde; die Lücke dazwischen zählt nicht.
    /// Die Blockgrößen gelten danach für das neue Gerät. Nur aufrufen, während kein Callback läuft.
    /// </summary>
    public void Restart()
    {
        _last = 0;
        Volatile.Write(ref _minFrames, int.MaxValue);
        Volatile.Write(ref _maxFrames, 0);
    }
}
