using JustPractise.Audio.Player;

namespace JustPractise.Audio.Tests.Player;

public sealed class DecodedAudioTests
{
    /// <summary>Schreibt Frames, deren Samples ihre eigene Nummer tragen: Frame n, Kanal c = n + c / 10.</summary>
    private static void Fill(DecodedAudio audio, long frames)
    {
        while (audio.Frames < frames)
        {
            var start = audio.Frames;
            var tail = audio.WritableTail((int)Math.Min(1000, (frames - start) * audio.Channels));
            if (tail.IsEmpty)
            {
                return;
            }

            for (var i = 0; i < tail.Length; i++)
            {
                tail[i] = start + (i / audio.Channels) + ((i % audio.Channels) / 10f);
            }

            audio.Commit(tail.Length);
        }
    }

    [Fact]
    public void ReadsAcrossBlockBoundaries()
    {
        var audio = new DecodedAudio(new BlockPool(blockSamples: 64), channels: 2, sampleRate: 48000);
        Fill(audio, 1000);

        var destination = new float[200];
        var read = audio.Read(25, destination);

        Assert.Equal(200, read);
        Assert.Equal(32, audio.FramesPerBlock);
        for (var i = 0; i < 100; i++)
        {
            Assert.Equal(25 + i, destination[2 * i]);
            Assert.Equal(25 + i + 0.1f, destination[(2 * i) + 1]);
        }
    }

    [Fact]
    public void KeepsFramesWholeWithOddChannelCounts()
    {
        var audio = new DecodedAudio(new BlockPool(blockSamples: 64), channels: 3, sampleRate: 48000);
        Fill(audio, 100);

        var destination = new float[3];
        audio.Read(21, destination);

        Assert.Equal(21, audio.FramesPerBlock);
        Assert.Equal([21f, 21.1f, 21.2f], destination);
    }

    [Fact]
    public void ReadsOnlyWhatIsAlreadyDecoded()
    {
        var audio = new DecodedAudio(new BlockPool(blockSamples: 64), channels: 2, sampleRate: 48000);
        Fill(audio, 50);

        var destination = new float[40];
        Assert.Equal(20, audio.Read(40, destination));
        Assert.Equal(0, audio.Read(50, destination));
        Assert.Equal(0, audio.Read(-1, destination));
    }

    [Fact]
    public void ReportsDurationAndCompletion()
    {
        var audio = new DecodedAudio(new BlockPool(blockSamples: 1024), channels: 1, sampleRate: 44100);
        Fill(audio, 44100);

        Assert.False(audio.IsComplete);
        audio.Complete();

        Assert.True(audio.IsComplete);
        Assert.False(audio.IsTruncated);
        Assert.Equal(TimeSpan.FromSeconds(1), audio.Duration);
    }

    [Fact]
    public void TruncatesFilesThatAreTooLong()
    {
        var audio = new DecodedAudio(new BlockPool(blockSamples: 4), channels: 2, sampleRate: 48000);

        Fill(audio, 10_000);

        Assert.Equal(DecodedAudio.MaxBlocks * 2, audio.Frames);
        Assert.True(audio.IsTruncated);
        Assert.True(audio.IsComplete);
    }

    [Fact]
    public void ReturnsItsBlocksToThePool()
    {
        var pool = new BlockPool(blockSamples: 64);
        var audio = new DecodedAudio(pool, channels: 2, sampleRate: 48000);
        Fill(audio, 100);
        var blocks = audio.BlockCount;

        audio.Release();

        Assert.Equal(4, blocks);
        Assert.Equal(blocks, pool.FreeBlocks);
        Assert.Equal(0, audio.Frames);
    }

    [Fact]
    public void ReadsWithoutAllocating()
    {
        var audio = new DecodedAudio(new BlockPool(blockSamples: 64), channels: 2, sampleRate: 48000);
        Fill(audio, 1000);
        var destination = new float[960];
        audio.Read(0, destination);

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var frame = 0; frame < 1000; frame += 7)
        {
            audio.Read(frame, destination);
        }

        Assert.Equal(before, GC.GetAllocatedBytesForCurrentThread());
    }

    [Fact]
    public async Task ReaderSeesOnlyFinishedFramesWhileDecoding()
    {
        const long Frames = 200_000;
        var audio = new DecodedAudio(new BlockPool(blockSamples: 4096), channels: 2, sampleRate: 48000);
        var cancellation = TestContext.Current.CancellationToken;

        var writer = Task.Run(() => Fill(audio, Frames), cancellation);
        var reader = Task.Run(
            () =>
            {
                var destination = new float[960];
                var errors = 0;
                while (!writer.IsCompleted)
                {
                    var frame = Math.Max(0, audio.Frames - 480);
                    var read = audio.Read(frame, destination);
                    for (var i = 0; i < read; i += 2)
                    {
                        errors += destination[i] == frame + (i / 2) ? 0 : 1;
                    }
                }

                return errors;
            },
            cancellation);

        await writer;
        Assert.Equal(0, await reader);
    }
}
