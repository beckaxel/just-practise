using System.Diagnostics;
using SoundFlow.Enums;

namespace JustPractise.Prototypes.SoundFlowCheck;

/// <summary>
/// Misst Größe und Abstände der Audio-Callbacks. Ein Abstand weit über der Puffergröße
/// deutet auf einen Aussetzer hin. Läuft auf dem Audio-Thread: keine Allokationen, keine Locks.
/// </summary>
internal sealed class CallbackStats(int channels, int sampleRate)
{
    private long _last;
    private long _count;
    private long _late;
    private long _maxTicks;
    private long _sumTicks;
    private int _frames;

    public void OnAudioProcessed(Span<float> samples, Capability capability)
    {
        var now = Stopwatch.GetTimestamp();
        _frames = samples.Length / channels;

        if (_last != 0)
        {
            var delta = now - _last;
            var expected = (double)_frames / sampleRate * Stopwatch.Frequency;
            _count++;
            _sumTicks += delta;
            _maxTicks = Math.Max(_maxTicks, delta);
            if (delta > expected * 1.8)
            {
                _late++;
            }
        }

        _last = now;
    }

    public void Reset()
    {
        _last = 0;
        _count = 0;
        _late = 0;
        _maxTicks = 0;
        _sumTicks = 0;
    }

    public override string ToString()
    {
        var count = _count;
        if (count == 0)
        {
            return "Callback: –";
        }

        var frames = _frames;
        var avg = _sumTicks * 1000.0 / count / Stopwatch.Frequency;
        var max = _maxTicks * 1000.0 / Stopwatch.Frequency;
        return $"Callback: {frames} Fr ({frames * 1000.0 / sampleRate:0.0} ms), Ø {avg:0.0} ms, max {max:0.0} ms, spät {_late}";
    }
}
