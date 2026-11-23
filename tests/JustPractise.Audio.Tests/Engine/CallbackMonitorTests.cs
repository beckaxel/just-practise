using System.Diagnostics;
using JustPractise.Audio.Engine;

namespace JustPractise.Audio.Tests.Engine;

public sealed class CallbackMonitorTests
{
    private const int SampleRate = 48000;
    private const int Frames = 480; // 10 ms

    private static readonly TimeSpan Period = TimeSpan.FromMilliseconds(10);

    /// <summary>
    /// Zeitpunkt in Ticks; die Tests beginnen nach der Anlaufzeit des Geräts. Gerundet, damit Abstände auf der Grenze
    /// auch mit der 10-MHz-Uhr von Windows genau auf der Grenze bleiben.
    /// </summary>
    private static long Ticks(double milliseconds) => (long)Math.Round((Startup + milliseconds) / 1000 * Stopwatch.Frequency);

    private static readonly double Startup = CallbackMonitor.StartupTime.TotalMilliseconds;

    /// <summary>Ein Monitor, dessen Gerät gleichmäßig angelaufen ist; der nächste Callback ist bei 0 ms fällig.</summary>
    private static CallbackMonitor Started(int frames = Frames, int sampleRate = SampleRate)
    {
        var monitor = new CallbackMonitor(Period);
        var step = Math.Max(Period.TotalMilliseconds, frames * 1000.0 / sampleRate);
        for (var time = -Startup; time < 0; time += step)
        {
            monitor.Tick(1 + Ticks(time), frames, sampleRate);
        }

        return monitor;
    }

    [Fact]
    public void IgnoresGapsWhileTheDeviceStartsUp()
    {
        // Unter PipeWire gemessen: nach dem ersten Callback 100 ms Pause, danach gleichmäßig.
        var monitor = new CallbackMonitor(Period);
        monitor.Tick(1 + Ticks(-Startup), Frames, SampleRate);
        for (var time = -Startup + 100; time < 200; time += 10)
        {
            monitor.Tick(1 + Ticks(time), Frames, SampleRate);
        }

        Assert.Equal(0, monitor.LateCallbacks);
        Assert.Equal(10, monitor.MaxGap.TotalMilliseconds, 0.01);

        monitor.Tick(1 + Ticks(230), Frames, SampleRate);
        Assert.Equal(1, monitor.LateCallbacks);
    }

    [Fact]
    public void CountsCallbacksThatComeMuchTooLate()
    {
        var monitor = Started();
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

    /// <summary>
    /// Wie unter Windows gemessen (44,1-kHz-Datei, 48-kHz-Mischer): gleich große Blöcke, im Mittel alle 10 ms, aber
    /// regelmäßig zwei kurz hintereinander und danach ein Abstand von bis zu 22 ms.
    /// </summary>
    private static List<double> ResampledTimes(int periods)
    {
        var times = new List<double>();
        for (var i = 0; i < periods; i++)
        {
            var due = i * 10.0;
            times.Add((i % 5) switch { 3 => due - 9, 4 => due + 2, _ => due });
        }

        times.Sort();
        return times;
    }

    [Fact]
    public void AcceptsTheUnevenPaceOfAResamplingDevice()
    {
        var monitor = new CallbackMonitor(Period);
        var times = ResampledTimes(10_000);

        foreach (var time in times)
        {
            monitor.Tick(1 + Ticks(time), 441, 44100);
        }

        Assert.Equal(0, monitor.LateCallbacks);
        Assert.Equal(21, monitor.MaxGap.TotalMilliseconds, 0.01);
        Assert.Equal(441, monitor.MinFrames);
        Assert.Equal(441, monitor.MaxFrames);
    }

    [Fact]
    public void CountsAStallWhileTheDeviceResamples()
    {
        var monitor = new CallbackMonitor(Period);
        var times = ResampledTimes(1_000);

        foreach (var time in times)
        {
            monitor.Tick(1 + Ticks(time + (time >= 5_000 ? 35 : 0)), 441, 44100);
        }

        Assert.Equal(1, monitor.LateCallbacks);
        Assert.True(monitor.MinLead.TotalMilliseconds < -8);
    }

    [Fact]
    public void ToleratesASlowDeviceClock()
    {
        // Das Gerät läuft 100 ppm langsamer als die Systemuhr; über eine Stunde wären das 360 ms.
        var monitor = new CallbackMonitor(Period);
        for (var i = 0; i < 360_000; i++)
        {
            monitor.Tick(1 + Ticks(i * 10.001), Frames, SampleRate);
        }

        Assert.Equal(0, monitor.LateCallbacks);
        Assert.True(monitor.MinLead.TotalMilliseconds > -1);
    }

    [Fact]
    public void StillCountsALongGapAfterASmallBlock()
    {
        var monitor = Started(441, 44100);
        monitor.Tick(1 + Ticks(0), 32, 44100);
        monitor.Tick(1 + Ticks(40), 441, 44100);

        Assert.Equal(1, monitor.LateCallbacks);
    }

    [Fact]
    public void MeasuresBlocksLongerThanThePeriodAgainstTheirOwnLength()
    {
        var monitor = Started(960);
        monitor.Tick(1 + Ticks(0), 960, SampleRate);
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
