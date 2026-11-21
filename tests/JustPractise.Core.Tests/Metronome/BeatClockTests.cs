using JustPractise.Core.Metronome;

namespace JustPractise.Core.Tests.Metronome;

public sealed class BeatClockTests
{
    private const int SampleRate = 48000;

    private static List<Beat> Beats(BeatClock clock, long endFrame)
    {
        var beats = new List<Beat>();
        while (clock.TryNextBeat(endFrame, out var beat))
        {
            beats.Add(beat);
        }

        return beats;
    }

    [Fact]
    public void PlaysBeatsInTheRightFramesWithTheOneAccented()
    {
        var clock = new BeatClock(SampleRate, 120, Meter.FourFour);
        clock.Start(1000);

        var beats = Beats(clock, 1000 + (5 * 24000) + 1);

        Assert.Equal([1000, 25000, 49000, 73000, 97000, 121000], beats.Select(beat => beat.Frame));
        Assert.Equal([0, 1, 2, 3, 0, 1], beats.Select(beat => beat.IndexInBar));
        Assert.Equal([true, false, false, false, true, false], beats.Select(beat => beat.Accented));
    }

    [Fact]
    public void CountsThreeBeatsInThreeFour()
    {
        var clock = new BeatClock(SampleRate, 60, Meter.ThreeFour);
        clock.Start(0);

        Assert.Equal([0, 1, 2, 0, 1], Beats(clock, (4 * SampleRate) + 1).Select(beat => beat.IndexInBar));
    }

    [Fact]
    public void DoesNotDriftWithFractionalBeatLengths()
    {
        // 70 BPM bei 48 kHz sind 41142,857… Samples pro Schlag. Nach 700 Schlägen (10 Minuten)
        // muss der Schlag genau auf Sample 28 800 000 liegen.
        var clock = new BeatClock(SampleRate, 70, Meter.FourFour);
        clock.Start(0);

        var beats = Beats(clock, 28_800_001);

        Assert.Equal(701, beats.Count);
        Assert.Equal(28_800_000, beats[^1].Frame);
    }

    [Fact]
    public void GivesTheSameBeatsBlockByBlock()
    {
        var whole = new BeatClock(44100, 97, Meter.ThreeFour);
        whole.Start(0);
        var expected = Beats(whole, 10 * 44100);

        var blockwise = new BeatClock(44100, 97, Meter.ThreeFour);
        blockwise.Start(0);
        var actual = new List<Beat>();
        for (long blockStart = 0; blockStart < 10 * 44100; blockStart += 441)
        {
            actual.AddRange(Beats(blockwise, blockStart + 441));
        }

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void AppliesANewTempoFromTheLastBeat()
    {
        var clock = new BeatClock(SampleRate, 60, Meter.FourFour);
        clock.Start(0);
        Assert.Single(Beats(clock, 10000));

        clock.SetTempo(120, 10000);

        Assert.Equal([24000, 48000], Beats(clock, 48001).Select(beat => beat.Frame));
    }

    [Fact]
    public void PlaysTheNextBeatAtOnceIfTheNewTempoIsAlreadyDue()
    {
        var clock = new BeatClock(SampleRate, 60, Meter.FourFour);
        clock.Start(0);
        Assert.Single(Beats(clock, 30000));

        clock.SetTempo(120, 30000);

        Assert.Equal([30000, 54000], Beats(clock, 54001).Select(beat => beat.Frame));
    }

    [Fact]
    public void KeepsTheFirstBeatIfTheTempoChangesBeforeIt()
    {
        var clock = new BeatClock(SampleRate, 60, Meter.FourFour);
        clock.Start(5000);

        clock.SetTempo(120, 0);

        Assert.Equal([5000, 29000], Beats(clock, 29001).Select(beat => beat.Frame));
    }

    [Fact]
    public void ContinuesTheBarCountAfterATempoChange()
    {
        var clock = new BeatClock(SampleRate, 60, Meter.FourFour);
        clock.Start(0);
        Beats(clock, SampleRate + 1);

        clock.SetTempo(120, SampleRate + 1);

        Assert.Equal([2, 3, 0], Beats(clock, SampleRate + 1 + (3 * 24000)).Select(beat => beat.IndexInBar));
    }

    [Fact]
    public void StartsANewBarIfTheMeterGetsShorter()
    {
        var clock = new BeatClock(SampleRate, 60, Meter.FourFour);
        clock.Start(0);
        Beats(clock, (2 * SampleRate) + 1);

        clock.SetMeter(Meter.ThreeFour);

        Assert.Equal([0, 1, 2, 0], Beats(clock, (7 * SampleRate) - 1).Select(beat => beat.IndexInBar));
    }

    [Fact]
    public void KeepsCountingIfTheMeterGetsLonger()
    {
        var clock = new BeatClock(SampleRate, 60, Meter.ThreeFour);
        clock.Start(0);
        Beats(clock, SampleRate + 1);

        clock.SetMeter(Meter.FourFour);

        Assert.Equal([2, 3, 0], Beats(clock, (5 * SampleRate) - 1).Select(beat => beat.IndexInBar));
    }

    [Fact]
    public void PlaysNothingWhenStoppedAndStartsWithTheOneAgain()
    {
        var clock = new BeatClock(SampleRate, 120, Meter.FourFour);
        Assert.Empty(Beats(clock, SampleRate));

        clock.Start(0);
        Beats(clock, 30000);
        clock.Stop();
        Assert.Empty(Beats(clock, 10 * SampleRate));

        clock.Start(10 * SampleRate);
        var beat = Assert.Single(Beats(clock, (10 * SampleRate) + 1));
        Assert.Equal(new Beat(10 * SampleRate, 0, true), beat);
    }

    [Fact]
    public void ClampsTheTempo()
    {
        Assert.Equal(BeatClock.MaxBpm, new BeatClock(SampleRate, 1000, Meter.FourFour).Bpm);
        Assert.Equal(BeatClock.MinBpm, new BeatClock(SampleRate, 0, Meter.FourFour).Bpm);
    }

    [Fact]
    public void AllocatesNothingWhileRunning()
    {
        var clock = new BeatClock(SampleRate, 250, Meter.ThreeFour);
        clock.Start(0);
        RunBlocks(clock, 0, 100);

        var before = GC.GetAllocatedBytesForCurrentThread();
        RunBlocks(clock, 100 * 480, 10_000);
        clock.SetTempo(100, 10_100 * 480);
        clock.SetMeter(Meter.FourFour);
        RunBlocks(clock, 10_100 * 480, 10_000);

        Assert.Equal(before, GC.GetAllocatedBytesForCurrentThread());
    }

    private static void RunBlocks(BeatClock clock, long start, int blocks)
    {
        for (var i = 0; i < blocks; i++)
        {
            while (clock.TryNextBeat(start + ((i + 1) * 480L), out _))
            {
            }
        }
    }
}
