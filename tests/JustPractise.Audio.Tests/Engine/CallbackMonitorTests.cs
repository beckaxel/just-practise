using System.Diagnostics;
using JustPractise.Audio.Engine;

namespace JustPractise.Audio.Tests.Engine;

public sealed class CallbackMonitorTests
{
    private const int SampleRate = 48000;
    private const int Frames = 480; // 10 ms

    private static long Ticks(double milliseconds) => (long)(milliseconds / 1000 * Stopwatch.Frequency);

    [Fact]
    public void CountsCallbacksThatComeMuchTooLate()
    {
        var monitor = new CallbackMonitor();
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
        var monitor = new CallbackMonitor();
        monitor.Tick(1, Frames, SampleRate);

        monitor.Restart();
        monitor.Tick(1 + Ticks(5000), Frames, SampleRate);
        monitor.Tick(1 + Ticks(5010), Frames, SampleRate);

        Assert.Equal(0, monitor.LateCallbacks);
    }
}
