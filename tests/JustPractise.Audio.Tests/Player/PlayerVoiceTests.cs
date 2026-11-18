using JustPractise.Audio.Player;

namespace JustPractise.Audio.Tests.Player;

// Braucht die native Bibliothek jpstretch; ohne sie werden die Tests übersprungen.
public sealed class PlayerVoiceTests : IDisposable
{
    private const int SampleRate = 48000;
    private const int BlockFrames = 480;

    private readonly List<LoadedTrack> _tracks = [];

    public PlayerVoiceTests()
    {
        Assert.SkipUnless(StretchVoice.IsAvailable(out var reason), reason);
    }

    public void Dispose() => _tracks.ForEach(track => track.Dispose());

    /// <summary>Drei Sinustöne mit gemeinsamer Periode von 0,1 s, damit sich Versatz eindeutig messen lässt.</summary>
    private static float Tones(long frame)
    {
        var t = (double)frame / SampleRate;
        return (float)(0.2 * (Math.Sin(2 * Math.PI * 440 * t) + Math.Sin(2 * Math.PI * 650 * t) + Math.Sin(2 * Math.PI * 1130 * t)));
    }

    private LoadedTrack Track(double seconds, Func<long, float> signal, int channels = 1, Func<int, float>? channelGain = null)
    {
        var frames = (long)(seconds * SampleRate);
        var audio = new DecodedAudio(new BlockPool(), channels, SampleRate);
        while (audio.Frames < frames)
        {
            var start = audio.Frames;
            var tail = audio.WritableTail((int)Math.Min(16384, (frames - start) * channels));
            for (var i = 0; i < tail.Length; i++)
            {
                tail[i] = signal(start + (i / channels)) * (channelGain?.Invoke(i % channels) ?? 1f);
            }

            audio.Commit(tail.Length);
        }

        audio.Complete();
        var track = new LoadedTrack(_tracks.Count + 1, audio, new StretchVoice(channels, SampleRate));
        _tracks.Add(track);
        return track;
    }

    private static PlayerVoice Loaded(LoadedTrack track)
    {
        var player = new PlayerVoice();
        player.Load(track, out _);
        return player;
    }

    /// <summary>Rechnet Blöcke zu 10 ms in Stereo und liefert linken und rechten Kanal.</summary>
    private static (float[] Left, float[] Right) Render(PlayerVoice player, double seconds)
    {
        var frames = (int)(seconds * SampleRate);
        var left = new float[frames];
        var right = new float[frames];
        var block = new float[BlockFrames * 2];
        for (var done = 0; done < frames; done += BlockFrames)
        {
            block.AsSpan().Clear();
            player.Render(block, 2);
            for (var i = 0; i < BlockFrames && done + i < frames; i++)
            {
                left[done + i] = block[2 * i];
                right[done + i] = block[(2 * i) + 1];
            }
        }

        return (left, right);
    }

    private static double Rms(ReadOnlySpan<float> samples)
    {
        var sum = 0.0;
        foreach (var sample in samples)
        {
            sum += sample * sample;
        }

        return Math.Sqrt(sum / samples.Length);
    }

    /// <summary>Der Versatz, bei dem <paramref name="output"/> am besten zum Signal ab <paramref name="signalStart"/> passt.</summary>
    private static int BestLag(float[] output, int outputStart, long signalStart, int length, int maxLag)
    {
        var best = 0;
        var bestScore = double.MinValue;
        for (var lag = -maxLag; lag <= maxLag; lag++)
        {
            var score = 0.0;
            for (var i = 0; i < length; i++)
            {
                score += output[outputStart + i] * Tones(signalStart + i - lag);
            }

            if (score > bestScore)
            {
                bestScore = score;
                best = lag;
            }
        }

        return best;
    }

    [Fact]
    public void StartsExactlyAtTheBeginningWithoutLatency()
    {
        var player = Loaded(Track(3, Tones));
        player.Play();

        var (left, _) = Render(player, 1);

        Assert.InRange(BestLag(left, 4800, 4800, 9600, maxLag: 1000), -2, 2);
        Assert.InRange(Rms(left.AsSpan(4800, 9600)) / Rms(Enumerable.Range(4800, 9600).Select(i => Tones(i)).ToArray()), 0.8, 1.2);
    }

    [Fact]
    public void JumpsExactlyToTheTarget()
    {
        var player = Loaded(Track(5, Tones));
        player.Play();
        Render(player, 0.5);

        player.Seek(2 * SampleRate);
        var (left, _) = Render(player, 0.5);

        // Erst wird 5 ms (240 Frames) ausgeblendet, danach beginnt die Zielstelle.
        Assert.InRange(BestLag(left, 4800, (2 * SampleRate) + 4800 - 240, 9600, maxLag: 1000), -2, 2);
    }

    [Fact]
    public void JumpsWhilePausedAndStartsThereLater()
    {
        var player = Loaded(Track(5, Tones));
        player.Seek(SampleRate);
        player.Play();

        var (left, _) = Render(player, 0.5);

        Assert.InRange(BestLag(left, 4800, SampleRate + 4800, 9600, maxLag: 1000), -2, 2);
    }

    [Fact]
    public void AdvancesHalfAsFastAtHalfTempo()
    {
        var player = Loaded(Track(5, Tones));
        player.SetTempo(0.5);
        player.Play();

        Render(player, 1);
        Assert.Equal(SampleRate / 2, player.Position);

        player.SetTempo(1.5);
        Render(player, 1);
        Assert.Equal(2 * SampleRate, player.Position);
    }

    [Fact]
    public void ClampsTheTempo()
    {
        var player = Loaded(Track(5, Tones));
        player.SetTempo(3);
        player.Play();

        Render(player, 1);

        Assert.Equal(SampleRate * 3 / 2, player.Position);
    }

    [Fact]
    public void StopsAtTheEnd()
    {
        var player = Loaded(Track(1, Tones));
        player.Play();

        var (left, _) = Render(player, 1.2);

        Assert.False(player.IsPlaying);
        Assert.Equal(SampleRate, player.Position);
        Assert.All(left.AsSpan(SampleRate + BlockFrames).ToArray(), sample => Assert.Equal(0f, sample));
    }

    [Fact]
    public void PlaysFromTheBeginningAgainAfterTheEnd()
    {
        var player = Loaded(Track(1, Tones));
        player.Play();
        Render(player, 1.2);

        player.Play();
        Render(player, 0.1);

        Assert.True(player.IsPlaying);
        Assert.Equal(SampleRate / 10, player.Position);
    }

    [Fact]
    public void FadesOutWhenPausedAndHoldsThePosition()
    {
        var player = Loaded(Track(5, Tones));
        player.Play();
        Render(player, 0.5);

        player.Pause();
        var (left, _) = Render(player, 0.5);

        Assert.False(player.IsPlaying);
        Assert.Equal((SampleRate / 2) + 240, player.Position);
        Assert.All(left.AsSpan(240).ToArray(), sample => Assert.Equal(0f, sample));
        Assert.True(Math.Abs(left[239]) < 0.05);
    }

    [Fact]
    public void FadesInWhenStarting()
    {
        var player = Loaded(Track(5, _ => 0.5f));
        player.Play();

        var (left, _) = Render(player, 0.1);

        Assert.True(Math.Abs(left[0]) < 0.01);
        Assert.True(Math.Abs(left[120]) < Math.Abs(left[2000]));
    }

    [Fact]
    public void AppliesTheVolumeSmoothly()
    {
        var loud = Loaded(Track(5, Tones));
        var quiet = Loaded(Track(5, Tones));
        quiet.SetVolume(-6);
        loud.Play();
        quiet.Play();

        var (loudLeft, _) = Render(loud, 0.5);
        var (quietLeft, _) = Render(quiet, 0.5);

        Assert.InRange(Rms(quietLeft.AsSpan(12000, 9600)) / Rms(loudLeft.AsSpan(12000, 9600)), 0.49, 0.52);
    }

    [Fact]
    public void PlaysMonoOnBothChannels()
    {
        var player = Loaded(Track(2, Tones));
        player.Play();

        var (left, right) = Render(player, 0.5);

        Assert.Equal(left, right);
        Assert.True(Rms(left) > 0.1);
    }

    [Fact]
    public void KeepsStereoChannelsApart()
    {
        var player = Loaded(Track(2, Tones, channels: 2, channelGain: channel => channel == 0 ? 1f : 0f));
        player.Play();

        var (left, right) = Render(player, 0.5);

        Assert.True(Rms(left) > 0.1);
        Assert.True(Rms(right) < 0.001);
    }

    [Fact]
    public void IsSilentWithoutATrack()
    {
        var player = new PlayerVoice();
        player.Play();

        var (left, _) = Render(player, 0.1);

        Assert.All(left, sample => Assert.Equal(0f, sample));
        Assert.Equal(0, player.TrackId);
    }

    [Fact]
    public void RendersWithoutAllocating()
    {
        var player = Loaded(Track(10, Tones));
        player.Play();
        Render(player, 0.5);
        var block = new float[BlockFrames * 2];

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 500; i++)
        {
            switch (i)
            {
                case 100:
                    player.SetTempo(0.7);
                    break;
                case 200:
                    player.Seek(3 * SampleRate);
                    break;
                case 300:
                    player.Pause();
                    break;
                case 350:
                    player.Play();
                    break;
                case 400:
                    player.SetVolume(-10);
                    break;
            }

            player.Render(block, 2);
        }

        Assert.Equal(before, GC.GetAllocatedBytesForCurrentThread());
    }
}
