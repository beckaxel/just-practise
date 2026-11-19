using System.Diagnostics;
using JustPractise.Audio.Engine;

namespace JustPractise.Audio.Tests.Engine;

public sealed class CallbackMonitorTests
{
    private const int SampleRate = 48000;
    private const int Frames = 480; // 10 ms

    private static readonly TimeSpan Period = TimeSpan.FromMilliseconds(10);

    private static long Ticks(double milliseconds) => (long)(milliseconds / 1000 * Stopwatch.Frequency);

    [Fact]
    public void CountsCallbacksThatComeMuchTooLate()
    {
        var monitor = new CallbackMonitor(Period);
        double[] times = [0, 10, 20, 30, 70, 80, 90, 108, 129];

        foreach (var time in times)
        {
            monitor.Tick(1 + Ticks(time), Frames, SampleRate);
        }

        Assert.Equal(2, monitor.LateCallbacks); // 40 ms und 21 ms Abstand; 18 ms (genau das 1,8-Fache) ist noch in Ordnung
        Assert.Equal(40, monitor.MaxGap.TotalMilliseconds, 0.01);
    }

    [Fact]
    public void IgnoresTheGapWhileTheDeviceWasClosed()
    {
        var monitor = new CallbackMonitor(Period);
        monitor.Tick(1, Frames, SampleRate);

        monitor.Restart();
        monitor.Tick(1 + Ticks(5000), Frames, SampleRate);
        monitor.Tick(1 + Ticks(5010), Frames, SampleRate);

        Assert.Equal(0, monitor.LateCallbacks);
    }

    [Fact]
    public void MeasuresSmallResampledBlocksAgainstThePeriod()
    {
        // Unter Windows bei 44,1 kHz Datei und 48-kHz-Mischer: kleine Blöcke im 10-ms-Takt, teils direkt hintereinander.
        var monitor = new CallbackMonitor(Period);
        (double Time, int Frames)[] callbacks = [(0, 441), (10, 32), (10.1, 409), (20, 441), (30, 16), (40, 425), (50, 441)];

        foreach (var (time, frames) in callbacks)
        {
            monitor.Tick(1 + Ticks(time), frames, 44100);
        }

        Assert.Equal(0, monitor.LateCallbacks);
        Assert.Equal(16, monitor.MinFrames);
        Assert.Equal(441, monitor.MaxFrames);
    }

    [Fact]
    public void StillCountsALongGapAfterASmallBlock()
    {
        var monitor = new CallbackMonitor(Period);
        monitor.Tick(1, 32, 44100);
        monitor.Tick(1 + Ticks(40), 441, 44100);

        Assert.Equal(1, monitor.LateCallbacks);
    }

    [Fact]
    public void MeasuresBlocksLongerThanThePeriodAgainstTheirOwnLength()
    {
        var monitor = new CallbackMonitor(Period);
        monitor.Tick(1, 960, SampleRate);
        monitor.Tick(1 + Ticks(20), 960, SampleRate);
        monitor.Tick(1 + Ticks(40), 960, SampleRate);

        Assert.Equal(0, monitor.LateCallbacks);
    }

    [Fact]
    public void ForgetsTheBlockSizesOfThePreviousDevice()
    {
        var monitor = new CallbackMonitor(Period);
        Assert.Equal(0, monitor.MinFrames);

        monitor.Tick(1, 32, 44100);
        monitor.Restart();
        monitor.Tick(1 + Ticks(5000), Frames, SampleRate);

        Assert.Equal(Frames, monitor.MinFrames);
        Assert.Equal(Frames, monitor.MaxFrames);
    }
}
