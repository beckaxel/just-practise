using System.Diagnostics;

namespace JustPractise.Prototypes.GcCheck;

/// <summary>
/// Misst die Rechenzeit pro Block im Audio-Callback. Hält ein GC den Thread mitten im Block an,
/// zeigt sich das hier als zu langer Block. Läuft auf dem Audio-Thread: keine Allokationen, keine Locks.
/// Die UI liest ohne Synchronisierung; ein gelegentlich halb aktualisierter Wert ist für die Anzeige egal.
/// </summary>
internal sealed class ProcessStats(int sampleRate)
{
    /// <summary>So lange nach dem Start zählt ein Block zur Startphase (JIT, erste Berechnungen).</summary>
    private const double StartupSeconds = 1.0;

    private long _blocks;
    private long _ticks;
    private long _maxTicks;
    private long _startupMaxTicks;
    private long _intervalMaxTicks;
    private long _overBudget;
    private long _frames;
    private int _blockFrames;

    public long Blocks => _blocks;

    public long OverBudget => Volatile.Read(ref _overBudget);

    public double BudgetMilliseconds => _blockFrames * 1000.0 / sampleRate;

    public double AverageMilliseconds => _blocks == 0 ? 0 : ToMilliseconds(_ticks) / _blocks;

    /// <summary>Längster Block nach der Startphase.</summary>
    public double MaxMilliseconds => ToMilliseconds(_maxTicks);

    /// <summary>Längster Block in der ersten Sekunde nach Start oder Zurücksetzen.</summary>
    public double StartupMaxMilliseconds => ToMilliseconds(_startupMaxTicks);

    public void Record(long ticks, int frames)
    {
        var startup = _frames < StartupSeconds * sampleRate;
        _blocks++;
        _ticks += ticks;
        _frames += frames;
        _blockFrames = frames;
        if (startup)
        {
            _startupMaxTicks = Math.Max(_startupMaxTicks, ticks);
        }
        else
        {
            _maxTicks = Math.Max(_maxTicks, ticks);
        }

        if (ticks > _intervalMaxTicks)
        {
            Volatile.Write(ref _intervalMaxTicks, ticks);
        }

        if (ticks > (double)frames / sampleRate * Stopwatch.Frequency)
        {
            Interlocked.Increment(ref _overBudget);
        }
    }

    /// <summary>Längster Block seit dem letzten Aufruf (für das Protokoll).</summary>
    public double TakeIntervalMaxMilliseconds() => ToMilliseconds(Interlocked.Exchange(ref _intervalMaxTicks, 0));

    public void Reset()
    {
        _blocks = 0;
        _ticks = 0;
        _maxTicks = 0;
        _startupMaxTicks = 0;
        _overBudget = 0;
        _frames = 0;
    }

    private static double ToMilliseconds(long ticks) => ticks * 1000.0 / Stopwatch.Frequency;
}
