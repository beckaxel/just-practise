namespace JustPractise.Core.Waveform;

/// <summary>
/// Berechnet <see cref="WaveformPeaks"/> aus Samples (interleaved), die in beliebig großen Stücken nacheinander kommen.
/// </summary>
/// <remarks>Die Länge muss vorab bekannt sein; das Array wird einmal in passender Größe angelegt.</remarks>
public sealed class WaveformPeaksBuilder
{
    private readonly int _channels;
    private readonly int _sampleRate;
    private readonly long _frames;
    private readonly short[] _data;
    private readonly int[] _levelStart;
    private readonly int[] _levelCount;
    private long _framesDone;
    private int _bucket;
    private int _bucketFill;
    private float _low = float.MaxValue;
    private float _high = float.MinValue;

    public WaveformPeaksBuilder(int channels, int sampleRate, long frames)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(channels);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sampleRate);
        ArgumentOutOfRangeException.ThrowIfNegative(frames);
        _channels = channels;
        _sampleRate = sampleRate;
        _frames = frames;

        var counts = new List<int>();
        var count = (int)((frames + WaveformPeaks.BaseFrames - 1) / WaveformPeaks.BaseFrames);
        counts.Add(count);
        while (count > 1)
        {
            count = (count + WaveformPeaks.LevelFactor - 1) / WaveformPeaks.LevelFactor;
            counts.Add(count);
        }

        _levelCount = [.. counts];
        _levelStart = new int[_levelCount.Length];
        var total = 0;
        for (var level = 0; level < _levelCount.Length; level++)
        {
            _levelStart[level] = total;
            total += _levelCount[level];
        }

        _data = new short[total * 2];
    }

    /// <summary>Nimmt die nächsten Samples auf, nur ganze Frames. Was über die angegebene Länge hinausgeht, zählt nicht.</summary>
    public void Append(ReadOnlySpan<float> samples)
    {
        var frames = (int)Math.Min(samples.Length / _channels, _frames - _framesDone);
        for (var frame = 0; frame < frames; frame++)
        {
            var offset = frame * _channels;
            for (var channel = 0; channel < _channels; channel++)
            {
                var sample = samples[offset + channel];
                _low = Math.Min(_low, sample);
                _high = Math.Max(_high, sample);
            }

            if (++_bucketFill == WaveformPeaks.BaseFrames)
            {
                CloseBucket();
            }
        }

        _framesDone += frames;
    }

    /// <summary>Schließt die Berechnung ab; fehlende Frames zählen als Stille.</summary>
    public WaveformPeaks Build()
    {
        if (_bucketFill > 0)
        {
            CloseBucket();
        }

        for (var level = 1; level < _levelCount.Length; level++)
        {
            var source = _levelStart[level - 1];
            var sourceCount = _levelCount[level - 1];
            var target = _levelStart[level];
            for (var bucket = 0; bucket < _levelCount[level]; bucket++)
            {
                var low = short.MaxValue;
                var high = short.MinValue;
                var first = bucket * WaveformPeaks.LevelFactor;
                var last = Math.Min(first + WaveformPeaks.LevelFactor, sourceCount);
                for (var child = first; child < last; child++)
                {
                    low = Math.Min(low, _data[(source + child) * 2]);
                    high = Math.Max(high, _data[((source + child) * 2) + 1]);
                }

                _data[(target + bucket) * 2] = low;
                _data[((target + bucket) * 2) + 1] = high;
            }
        }

        return new WaveformPeaks(_data, _levelStart, _levelCount, _frames, _sampleRate);
    }

    private void CloseBucket()
    {
        if (_bucket < _levelCount[0])
        {
            _data[_bucket * 2] = ToShort(_low);
            _data[(_bucket * 2) + 1] = ToShort(_high);
        }

        _bucket++;
        _bucketFill = 0;
        _low = float.MaxValue;
        _high = float.MinValue;
    }

    private static short ToShort(float value) => (short)Math.Round(Math.Clamp(value, -1f, 1f) * short.MaxValue);
}
