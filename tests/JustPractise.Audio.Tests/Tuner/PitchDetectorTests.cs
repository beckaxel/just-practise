using JustPractise.Audio.Tuner;
using JustPractise.Core.Tuner;

namespace JustPractise.Audio.Tests.Tuner;

public sealed class PitchDetectorTests
{
    private const int SampleRate = 48000;
    private const int Window = 4096;

    private readonly PitchDetector _detector = new(SampleRate, Window);

    /// <summary>Ein Ton mit Obertönen wie ein Instrument; <paramref name="harmonics"/> sind die Pegel ab dem Grundton.</summary>
    private static float[] Tone(double frequency, double[] harmonics, double amplitude = 0.3, int seed = 0, double noise = 0)
    {
        var random = new Random(seed);
        var samples = new float[Window];
        for (var i = 0; i < samples.Length; i++)
        {
            var t = (double)i / SampleRate;
            var value = 0.0;
            for (var h = 0; h < harmonics.Length; h++)
            {
                value += harmonics[h] * Math.Sin((2 * Math.PI * frequency * (h + 1) * t) + h);
            }

            samples[i] = (float)((amplitude * value) + (noise * ((random.NextDouble() * 2) - 1)));
        }

        return samples;
    }

    private static double CentsOff(double measured, double expected) => 1200 * Math.Log2(measured / expected);

    [Theory]
    [InlineData(41.2034)] // E1, tiefe Saite am Bass
    [InlineData(55.0)]
    [InlineData(110.0)]
    [InlineData(233.0819)] // B3, Tenorsax klingend
    [InlineData(440.0)]
    [InlineData(987.7666)]
    [InlineData(2093.005)]
    [InlineData(4186.009)] // C8
    public void MeasuresAPureToneWithinOneCent(double frequency)
    {
        var reading = _detector.Detect(Tone(frequency, [1.0]));

        Assert.True(reading.HasPitch);
        Assert.InRange(CentsOff(reading.Frequency, frequency), -1, 1);
        Assert.True(reading.Clarity > 0.95);
    }

    [Theory]
    [InlineData(82.4069)] // E2, Gitarre
    [InlineData(146.8324)]
    [InlineData(392.0)]
    public void FindsTheFundamentalOfABrightToneWithoutOctaveErrors(double frequency)
    {
        // Stärkere Obertöne als der Grundton, wie bei Saxophon, Gitarre oder Geige
        var reading = _detector.Detect(Tone(frequency, [0.4, 1.0, 0.8, 0.6, 0.5, 0.3]));

        Assert.True(reading.HasPitch);
        Assert.InRange(CentsOff(reading.Frequency, frequency), -2, 2);
    }

    [Fact]
    public void HearsTheDeviationOfASlightlyOutOfTuneNote()
    {
        var frequency = 440 * Math.Pow(2, 7 / 1200.0); // 7 Cent zu hoch

        var note = TunedNote.FromFrequency(_detector.Detect(Tone(frequency, [1.0, 0.5, 0.25])).Frequency);

        Assert.Equal(69, note.Midi);
        Assert.InRange(note.Cents, 6, 8);
    }

    [Fact]
    public void StaysSilentBelowTheLevelGate()
    {
        var reading = _detector.Detect(Tone(440, [1.0], amplitude: 0.0005));

        Assert.False(reading.HasPitch);
        Assert.True(reading.LevelDb < -60);
    }

    [Fact]
    public void FindsNoPitchInNoise()
    {
        var reading = _detector.Detect(Tone(440, [0.0], noise: 0.3, seed: 7));

        Assert.False(reading.HasPitch);
        Assert.True(reading.LevelDb > -20);
    }

    [Fact]
    public void ToleratesSomeNoiseOnATone()
    {
        var reading = _detector.Detect(Tone(196.0, [1.0, 0.6, 0.3], noise: 0.05, seed: 3));

        Assert.True(reading.HasPitch);
        Assert.InRange(CentsOff(reading.Frequency, 196.0), -3, 3);
    }

    [Fact]
    public void ReportsTheLevelInDecibels()
    {
        var reading = _detector.Detect(Tone(440, [1.0], amplitude: 1.0));

        Assert.Equal(-3.01, reading.LevelDb, 1); // Sinus mit Spitze 1: RMS 1/√2
    }

    [Fact]
    public void MeasuresTheLastWindowOfALongerBuffer()
    {
        var older = Tone(220, [1.0]);
        var newer = Tone(330, [1.0]);

        var reading = _detector.Detect([.. older, .. newer]);

        Assert.InRange(CentsOff(reading.Frequency, 330), -1, 1);
    }
}

public sealed class InputRingTests
{
    [Fact]
    public void KeepsTheLatestSamplesAcrossTheWrapAround()
    {
        var ring = new InputRing(8);
        var latest = new float[4];
        Assert.False(ring.CopyLatest(latest));

        ring.Write([1, 2, 3, 4, 5, 6], channels: 1);
        ring.Write([7, 8, 9, 10], channels: 1);

        Assert.True(ring.CopyLatest(latest));
        Assert.Equal([7f, 8f, 9f, 10f], latest);
        Assert.Equal(10, ring.Written);
    }

    [Fact]
    public void TakesTheFirstChannelOfInterleavedInput()
    {
        var ring = new InputRing(8);
        ring.Write([1, -1, 2, -2, 3, -3], channels: 2);

        var latest = new float[3];
        Assert.True(ring.CopyLatest(latest));
        Assert.Equal([1f, 2f, 3f], latest);
    }
}
