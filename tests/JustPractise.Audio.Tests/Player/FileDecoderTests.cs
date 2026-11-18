using System.Text;
using JustPractise.Audio.Player;
using SoundFlow.Backends.MiniAudio;

namespace JustPractise.Audio.Tests.Player;

// MiniAudioEngine öffnet hier kein Gerät, es wird nur zum Decodieren gebraucht.
public sealed class FileDecoderTests : IDisposable
{
    private readonly MiniAudioEngine _engine = new();
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "jp-tests-" + Guid.NewGuid().ToString("N"));

    public FileDecoderTests() => Directory.CreateDirectory(_directory);

    public void Dispose()
    {
        _engine.Dispose();
        Directory.Delete(_directory, recursive: true);
    }

    /// <summary>Schreibt eine 16-Bit-WAV-Datei. Frame n, Kanal c hat den Wert (n % 1000 + 100 c) / 32768.</summary>
    private string Wav(string name, int channels, int sampleRate, int frames)
    {
        var path = Path.Combine(_directory, name);
        using var writer = new BinaryWriter(File.Create(path));
        var dataBytes = frames * channels * 2;
        writer.Write(Encoding.ASCII.GetBytes("RIFF"));
        writer.Write(36 + dataBytes);
        writer.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
        writer.Write(16);
        writer.Write((short)1);
        writer.Write((short)channels);
        writer.Write(sampleRate);
        writer.Write(sampleRate * channels * 2);
        writer.Write((short)(channels * 2));
        writer.Write((short)16);
        writer.Write(Encoding.ASCII.GetBytes("data"));
        writer.Write(dataBytes);
        for (var frame = 0; frame < frames; frame++)
        {
            for (var channel = 0; channel < channels; channel++)
            {
                writer.Write((short)((frame % 1000) + (100 * channel)));
            }
        }

        return path;
    }

    // miniaudio teilt 16-Bit-Samples durch 32767 statt 32768, das macht bis zu 1e-6 aus.
    private const float Tolerance = 1e-5f;

    private static float Expected(long frame, int channel) => ((frame % 1000) + (100 * channel)) / 32768f;

    private DecodedAudio Decode(string path, BlockPool? pool = null)
    {
        DecodedAudio? audio = null;
        new FileDecoder(_engine, pool ?? new BlockPool(blockSamples: 8192)).Decode(path, opened => audio = opened, TestContext.Current.CancellationToken);
        return audio!;
    }

    [Fact]
    public void DecodesStereoAtTheOriginalRate()
    {
        var audio = Decode(Wav("stereo.wav", channels: 2, sampleRate: 48000, frames: 100_000));

        Assert.Equal(2, audio.Channels);
        Assert.Equal(48000, audio.SampleRate);
        Assert.Equal(100_000, audio.Frames);
        Assert.True(audio.IsComplete);

        var destination = new float[20];
        audio.Read(54_321, destination);
        for (var i = 0; i < 10; i++)
        {
            Assert.Equal(Expected(54_321 + i, 0), destination[2 * i], Tolerance);
            Assert.Equal(Expected(54_321 + i, 1), destination[(2 * i) + 1], Tolerance);
        }
    }

    [Fact]
    public void DecodesMonoAt44100()
    {
        var audio = Decode(Wav("mono.wav", channels: 1, sampleRate: 44100, frames: 44100));

        Assert.Equal(1, audio.Channels);
        Assert.Equal(44100, audio.SampleRate);
        Assert.Equal(TimeSpan.FromSeconds(1), audio.Duration);

        var destination = new float[3];
        audio.Read(1999, destination);
        Assert.Equal(Expected(1999, 0), destination[0], Tolerance);
        Assert.Equal(Expected(2000, 0), destination[1], Tolerance);
        Assert.Equal(Expected(2001, 0), destination[2], Tolerance);
    }

    [Fact]
    public void HandsTheAudioOverBeforeDecodingFinishes()
    {
        var path = Wav("early.wav", channels: 2, sampleRate: 48000, frames: 48000);
        long framesWhenOpened = -1;
        var completeWhenOpened = true;

        new FileDecoder(_engine, new BlockPool()).Decode(
            path,
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
        var path = Wav("cancel.wav", channels: 2, sampleRate: 48000, frames: 480_000);
        using var cancellation = new CancellationTokenSource();
        DecodedAudio? audio = null;

        Assert.Throws<OperationCanceledException>(() => new FileDecoder(_engine, new BlockPool(blockSamples: 8192)).Decode(
            path,
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
    public void RejectsFilesThatAreNotAudio()
    {
        var path = Path.Combine(_directory, "notes.mp3");
        File.WriteAllText(path, "Das ist keine Audiodatei, nur Text mit der falschen Endung.");

        Assert.Throws<InvalidDataException>(() => Decode(path));
    }

    [Fact]
    public void ReusesTheBlocksOfThePreviousFile()
    {
        var pool = new BlockPool(blockSamples: 8192);
        var first = Decode(Wav("first.wav", channels: 2, sampleRate: 48000, frames: 48000), pool);
        var blocks = first.BlockCount;
        first.Release();

        var second = Decode(Wav("second.wav", channels: 2, sampleRate: 48000, frames: 24000), pool);

        Assert.Equal(blocks - second.BlockCount, pool.FreeBlocks);
    }
}
