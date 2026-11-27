using JustPractise.Audio.Player;

namespace JustPractise.Audio.Tests.Player;

// Die Testdateien liegen in Assets/ (siehe Assets/README.md).
public sealed class AacDecoderTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "jp-tests-" + Guid.NewGuid().ToString("N"));

    public AacDecoderTests()
    {
        Assert.SkipUnless(AacDecoder.IsAvailable(out var reason), reason);
        Directory.CreateDirectory(_directory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private static string Asset(string name) => Path.Combine(AppContext.BaseDirectory, "Assets", name);

    private static DecodedAudio Decode(string path, BlockPool? pool = null)
    {
        DecodedAudio? audio = null;
        new AacDecoder().Decode(path, pool ?? new BlockPool(blockSamples: 8192), opened => audio = opened, TestContext.Current.CancellationToken);
        return audio!;
    }

    private static float[] ReadAll(DecodedAudio audio)
    {
        var samples = new float[audio.Frames * audio.Channels];
        audio.Read(0, samples);
        return samples;
    }

    [Fact]
    public void DecodesAlacWithoutLoss()
    {
        var audio = Decode(Asset("pattern-alac.m4a"));

        Assert.Equal(2, audio.Channels);
        Assert.Equal(44100, audio.SampleRate);
        Assert.Equal(20_000, audio.Frames);
        Assert.True(audio.IsComplete);

        // Über mehrere Blöcke des Pools hinweg (8192 Samples je Block) genau die Werte der WAV-Vorlage.
        var samples = ReadAll(audio);
        for (var frame = 0; frame < audio.Frames; frame++)
        {
            Assert.Equal(TestWav.Expected(frame, 0), samples[2 * frame]);
            Assert.Equal(TestWav.Expected(frame, 1), samples[(2 * frame) + 1]);
        }
    }

    [Fact]
    public void DecodesAacInM4aWithTheEncoderDelayRemoved()
    {
        var audio = Decode(Asset("sine-440-aac.m4a"));

        Assert.Equal(2, audio.Channels);
        Assert.Equal(48000, audio.SampleRate);
        Assert.Equal(48000, audio.Frames);
        AssertSine440(audio);
    }

    [Fact]
    public void DecodesRawAac()
    {
        var audio = Decode(Asset("sine-440-adts.aac"));

        Assert.Equal(2, audio.Channels);
        Assert.Equal(48000, audio.SampleRate);
        // Rohes AAC kennt die Encoder-Verzögerung nicht, es kommen etwas mehr als 48000 Frames.
        Assert.InRange(audio.Frames, 48000, 48000 + 2048);
        AssertSine440(audio);
    }

    [Fact]
    public void HandsTheAudioOverBeforeDecodingFinishes()
    {
        long framesWhenOpened = -1;
        var completeWhenOpened = true;

        new AacDecoder().Decode(
            Asset("sine-440-aac.m4a"),
            new BlockPool(),
            audio =>
            {
                framesWhenOpened = audio.Frames;
                completeWhenOpened = audio.IsComplete;
            },
            TestContext.Current.CancellationToken);

        Assert.Equal(0, framesWhenOpened);
        Assert.False(completeWhenOpened);
    }

    [Fact]
    public void StopsWhenCancelled()
    {
        using var cancellation = new CancellationTokenSource();
        DecodedAudio? audio = null;

        Assert.Throws<OperationCanceledException>(() => new AacDecoder().Decode(
            Asset("sine-440-aac.m4a"),
            new BlockPool(blockSamples: 8192),
            opened =>
            {
                audio = opened;
                cancellation.Cancel();
            },
            cancellation.Token));

        Assert.NotNull(audio);
        Assert.False(audio.IsComplete);
    }

    [Fact]
    public void ReportsAMissingFile() =>
        Assert.Throws<FileNotFoundException>(() => Decode(Path.Combine(_directory, "missing.m4a")));

    [Fact]
    public void RejectsFilesThatAreNotAudio()
    {
        var path = Path.Combine(_directory, "notes.m4a");
        File.WriteAllText(path, "Das ist keine Audiodatei, nur Text mit der falschen Endung.");

        Assert.Throws<InvalidDataException>(() => Decode(path));
    }

    [Fact]
    public void ReadsPathsWithUmlautsAndSharps()
    {
        var path = Path.Combine(_directory, "Übung in F♯ – Frühling.m4a");
        File.Copy(Asset("pattern-alac.m4a"), path);

        Assert.Equal(20_000, Decode(path).Frames);
    }

    // Ein Sinus von 440 Hz mit halber Aussteuerung: Nulldurchgänge und Effektivwert in der Mitte der Datei.
    private static void AssertSine440(DecodedAudio audio)
    {
        var samples = ReadAll(audio);
        var start = audio.SampleRate / 4;
        var end = audio.SampleRate * 3 / 4;
        var crossings = 0;
        var sumSquares = 0.0;
        for (var frame = start; frame < end; frame++)
        {
            var left = samples[frame * audio.Channels];
            var previous = samples[(frame - 1) * audio.Channels];
            if (previous < 0 && left >= 0)
            {
                crossings++;
            }

            sumSquares += left * (double)left;
        }

        var seconds = (double)(end - start) / audio.SampleRate;
        Assert.InRange(crossings / seconds, 436, 444);
        Assert.InRange(Math.Sqrt(sumSquares / (end - start)), 0.33, 0.38);
    }
}
