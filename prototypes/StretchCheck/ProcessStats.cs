using System.Diagnostics;

namespace JustPractise.Prototypes.StretchCheck;

/// <summary>
/// Misst die Rechenzeit pro Block im Verhältnis zur Dauer des Blocks.
/// Läuft auf dem Audio-Thread: keine Allokationen, keine Locks.
/// </summary>
internal sealed class ProcessStats(int sampleRate)
{
    private long _blocks;
    private long _ticks;
    private long _maxTicks;
    private long _audioFrames;
    private long _overBudget;
    private int _blockFrames;

    public void Record(long ticks, int frames)
    {
        _blocks++;
        _ticks += ticks;
        _maxTicks = Math.Max(_maxTicks, ticks);
        _audioFrames += frames;
        _blockFrames = frames;
        if (ticks > (double)frames / sampleRate * Stopwatch.Frequency)
        {
            _overBudget++;
        }
    }

    public void Reset()
    {
        _blocks = 0;
        _ticks = 0;
        _maxTicks = 0;
        _audioFrames = 0;
        _overBudget = 0;
    }

    public double TotalSeconds => (double)_ticks / Stopwatch.Frequency;

    /// <summary>Wie viel schneller als Echtzeit gerechnet wird.</summary>
    public double RealtimeFactor => _ticks == 0 ? 0 : (double)_audioFrames / sampleRate / TotalSeconds;

    public double MaxMilliseconds => _maxTicks * 1000.0 / Stopwatch.Frequency;

    public double BudgetMilliseconds => _blockFrames * 1000.0 / sampleRate;

    public long OverBudget => _overBudget;

    public override string ToString()
    {
        if (_blocks == 0)
        {
            return "Last: –";
        }

        var average = TotalSeconds * 1000.0 / _blocks;
        return $"Last: Ø {average:0.00} ms, max {MaxMilliseconds:0.00} ms von {BudgetMilliseconds:0.0} ms, " +
            $"{RealtimeFactor:0}x Echtzeit, über Budget {_overBudget}";
    }
}
