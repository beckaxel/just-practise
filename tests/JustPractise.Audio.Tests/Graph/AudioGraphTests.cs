using JustPractise.Audio.Graph;
using JustPractise.Audio.Metronome;
using JustPractise.Audio.Player;
using JustPractise.Core.Metronome;

namespace JustPractise.Audio.Tests.Graph;

public sealed class AudioGraphTests
{
    private const int SampleRate = 48000;
    private const int Channels = 2;
    private const int BlockFrames = 480;

    private readonly ClickSound _sound = ClickSound.Create(SampleRate);
    private readonly CommandRing<AudioCommand> _commands = new(64);
    private readonly AudioStatus _status = new();
    private readonly AudioGraph _graph;

    public AudioGraphTests()
    {
        var click = new ClickVoice(new BeatClock(SampleRate, 120, Meter.FourFour), _sound);
        _graph = new AudioGraph(Channels, new PlayerVoice(), click, _commands, new CommandRing<RetiredTrack>(4), _status);
    }

    /// <summary>Rechnet Blöcke zu 10 ms und liefert den linken Kanal.</summary>
    private float[] Render(int frames)
    {
        var left = new float[frames];
        var block = new float[BlockFrames * Channels];
        for (var done = 0; done < frames; done += BlockFrames)
        {
            _graph.Render(block);
            for (var i = 0; i < BlockFrames && done + i < frames; i++)
            {
                left[done + i] = block[i * Channels];
            }
        }

        return left;
    }

    private void Send(AudioCommand command) => Assert.True(_commands.TryEnqueue(command));

    [Fact]
    public void IsSilentWithoutMetronome()
    {
        Assert.All(Render(SampleRate), sample => Assert.Equal(0f, sample));
        Assert.Equal(SampleRate, _status.RenderedFrames);
    }

    [Fact]
    public void PlaysTheClicksOnTheExactFrames()
    {
        Send(AudioCommand.MetronomeStart());

        var left = Render(SampleRate);

        var accented = _sound.Accented.Span;
        var normal = _sound.Normal.Span;
        Assert.Equal(accented.ToArray(), left.AsSpan(0, accented.Length).ToArray());
        Assert.Equal(normal.ToArray(), left.AsSpan(24000, normal.Length).ToArray());
        Assert.All(left.AsSpan(accented.Length, 24000 - accented.Length).ToArray(), sample => Assert.Equal(0f, sample));
    }

    [Fact]
    public void PlaysTheSameOnBothChannels()
    {
        Send(AudioCommand.MetronomeStart());
        var block = new float[BlockFrames * Channels];

        _graph.Render(block);

        for (var i = 0; i < BlockFrames; i++)
        {
            Assert.Equal(block[i * 2], block[(i * 2) + 1]);
        }
    }

    [Fact]
    public void ContinuesAClickAcrossTheBlockBoundary()
    {
        Send(AudioCommand.MetronomeTempo(97));
        Send(AudioCommand.MetronomeStart());

        var left = Render(3 * SampleRate);

        // 97 BPM: Schlag 2 beginnt bei Sample 29691 (gerundet), mitten in einem Block.
        var beat = (int)Math.Round(60.0 * SampleRate / 97);
        Assert.NotEqual(0, beat % BlockFrames);
        Assert.Equal(_sound.Normal.ToArray(), left.AsSpan(beat, _sound.Normal.Length).ToArray());
    }

    [Fact]
    public void AccentsTheOneInThreeFour()
    {
        Send(AudioCommand.MetronomeMeter(Meter.ThreeFour));
        Send(AudioCommand.MetronomeStart());

        var left = Render(2 * SampleRate);

        Assert.Equal(_sound.Accented.ToArray(), left.AsSpan(3 * 24000, _sound.Accented.Length).ToArray());
    }

    [Fact]
    public void AppliesTheVolume()
    {
        Send(AudioCommand.MetronomeVolume(-6));
        Send(AudioCommand.MetronomeStart());

        var left = Render(BlockFrames);

        var expected = _sound.Accented.Span[10] * Decibels.ToGain(-6);
        Assert.Equal(expected, left[10]);
        Assert.InRange(Decibels.ToGain(-6), 0.50f, 0.51f);
    }

    [Fact]
    public void StopsImmediatelyAndReportsIt()
    {
        Send(AudioCommand.MetronomeStart());
        Render(BlockFrames);
        Assert.True(_status.MetronomeRunning);
        Assert.Equal(new BeatInfo(0, 0), _status.LastBeat);

        Send(AudioCommand.MetronomeStop());
        var left = Render(SampleRate);

        Assert.All(left, sample => Assert.Equal(0f, sample));
        Assert.False(_status.MetronomeRunning);
        Assert.Null(_status.LastBeat);
    }

    [Fact]
    public void ReportsTheLastBeat()
    {
        Send(AudioCommand.MetronomeStart());

        Render(SampleRate + BlockFrames);

        Assert.Equal(new BeatInfo(48000, 2), _status.LastBeat);
    }

    [Fact]
    public void StartsWithTheOneAtTheNextBlock()
    {
        Render(10 * BlockFrames);
        Send(AudioCommand.MetronomeStart());

        Render(BlockFrames);

        Assert.Equal(new BeatInfo(10 * BlockFrames, 0), _status.LastBeat);
    }

    [Fact]
    public void RendersWithoutAllocating()
    {
        var block = new float[BlockFrames * Channels];
        Send(AudioCommand.MetronomeStart());
        for (var i = 0; i < 200; i++)
        {
            _graph.Render(block);
        }

        Send(AudioCommand.MetronomeTempo(200));
        Send(AudioCommand.MetronomeMeter(Meter.ThreeFour));
        Send(AudioCommand.MetronomeVolume(-3));
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 2000; i++)
        {
            _graph.Render(block);
        }

        Assert.Equal(before, GC.GetAllocatedBytesForCurrentThread());
    }
}
