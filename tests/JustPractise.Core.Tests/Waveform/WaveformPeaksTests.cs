using JustPractise.Core.Waveform;

namespace JustPractise.Core.Tests.Waveform;

public sealed class WaveformPeaksTests
{
    private const int SampleRate = 48000;

    /// <summary>Stereo: links <paramref name="left"/>, rechts <paramref name="right"/> für jeden Frame.</summary>
    private static float[] Stereo(int frames, Func<int, float> left, Func<int, float> right)
    {
        var samples = new float[frames * 2];
        for (var i = 0; i < frames; i++)
        {
            samples[i * 2] = left(i);
            samples[(i * 2) + 1] = right(i);
        }

        return samples;
    }

    private static (float[] Min, float[] Max) Envelope(WaveformPeaks peaks, double start, double framesPerColumn, int columns)
    {
        var min = new float[columns];
        var max = new float[columns];
        peaks.Envelope(start, framesPerColumn, min, max);
        return (min, max);
    }

    [Fact]
    public void TakesTheExtremesOfAllChannels()
    {
        var builder = new WaveformPeaksBuilder(2, SampleRate, 128);
        builder.Append(Stereo(128, i => i == 10 ? 0.5f : 0f, i => i == 100 ? -0.25f : 0f));

        var (min, max) = Envelope(builder.Build(), 0, 64, 2);

        Assert.Equal([0f, -0.25f], min, new ToleranceComparer());
        Assert.Equal([0.5f, 0f], max, new ToleranceComparer());
    }

    [Fact]
    public void AcceptsSamplesInPiecesOfAnySize()
    {
        var whole = new WaveformPeaksBuilder(1, SampleRate, 1000);
        var pieces = new WaveformPeaksBuilder(1, SampleRate, 1000);
        var samples = Enumerable.Range(0, 1000).Select(i => MathF.Sin(i * 0.05f)).ToArray();

        whole.Append(samples);
        for (var offset = 0; offset < samples.Length; offset += 37)
        {
            pieces.Append(samples.AsSpan(offset, Math.Min(37, samples.Length - offset)));
        }

        var expected = Envelope(whole.Build(), 0, 10, 100);
        var actual = Envelope(pieces.Build(), 0, 10, 100);
        Assert.Equal(expected.Min, actual.Min);
        Assert.Equal(expected.Max, actual.Max);
    }

    [Fact]
    public void CoarseLevelsKeepASinglePeak()
    {
        const int frames = SampleRate * 60;
        var builder = new WaveformPeaksBuilder(1, SampleRate, frames);
        var samples = new float[frames];
        samples[1_234_567] = 0.9f;
        builder.Append(samples);
        var peaks = builder.Build();

        // Das ganze Stück in 100 Spalten: Die Spalte mit der Spitze zeigt sie. Ein grober Abschnitt kann über die
        // Spaltengrenze reichen, dann zeigt sie auch die Nachbarspalte; alle anderen sind still.
        var (_, max) = Envelope(peaks, 0, frames / 100.0, 100);
        var column = (int)(1_234_567 / (frames / 100.0));

        Assert.True(peaks.Levels > 5);
        Assert.Equal(0.9f, max[column], 3);
        Assert.InRange(max.Count(value => value == 0f), 98, 99);
        Assert.All(max.Where((_, i) => Math.Abs(i - column) > 1), value => Assert.Equal(0f, value));
    }

    [Fact]
    public void ColumnsOutsideTheFileAreSilent()
    {
        var builder = new WaveformPeaksBuilder(1, SampleRate, 256);
        builder.Append(Enumerable.Repeat(0.5f, 256).ToArray());

        var (min, max) = Envelope(builder.Build(), -128, 64, 8);

        Assert.Equal([0f, 0f, 0.5f, 0.5f, 0.5f, 0.5f, 0f, 0f], max, new ToleranceComparer());
        Assert.Equal([0f, 0f, 0.5f, 0.5f, 0.5f, 0.5f, 0f, 0f], min, new ToleranceComparer());
    }

    [Fact]
    public void ColumnsFinerThanTheBaseLevelRepeatTheirSection()
    {
        var builder = new WaveformPeaksBuilder(1, SampleRate, 128);
        builder.Append(Stereo(64, _ => 0.25f, _ => 0.75f)); // als Mono: 128 Frames, erst 0,25 und 0,75 im Wechsel

        var (_, max) = Envelope(builder.Build(), 0, 16, 8);

        Assert.All(max, value => Assert.Equal(0.75f, value, 3));
    }

    [Fact]
    public void MissingFramesCountAsSilence()
    {
        var builder = new WaveformPeaksBuilder(1, SampleRate, 256);
        builder.Append(Enumerable.Repeat(0.5f, 64).ToArray());

        var peaks = builder.Build();
        var (_, max) = Envelope(peaks, 0, 64, 4);

        Assert.Equal(256, peaks.Frames);
        Assert.Equal([0.5f, 0f, 0f, 0f], max, new ToleranceComparer());
    }

    [Fact]
    public void AnEmptyFileHasSilentColumns()
    {
        var peaks = new WaveformPeaksBuilder(2, SampleRate, 0).Build();

        var (min, max) = Envelope(peaks, 0, 100, 3);

        Assert.Equal([0f, 0f, 0f], min);
        Assert.Equal([0f, 0f, 0f], max);
    }

    private sealed class ToleranceComparer : IEqualityComparer<float>
    {
        public bool Equals(float x, float y) => Math.Abs(x - y) < 0.001f;

        public int GetHashCode(float obj) => 0;
    }
}
