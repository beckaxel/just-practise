using System.Diagnostics;
using JustPractise.Audio.Engine;

namespace JustPractise.Audio.Tests.Engine;

public sealed class FallbackOutputTests
{
    [Fact]
    public void UsesTheRealDeviceWhenItOpens()
    {
        using var real = new FakeOutput();
        using var spare = new FakeOutput();
        using var output = new FallbackOutput(real, spare);

        output.Open(48000, 2, _ => { });

        Assert.True(output.HasDevice);
        Assert.Equal("Fake", output.DeviceName);
        Assert.Null(output.Failure);
        Assert.False(spare.IsOpen);
    }

    [Fact]
    public void FallsBackWhenTheRealDeviceFails()
    {
        using var spare = new FakeOutput();
        using var output = new FallbackOutput(new BrokenOutput(), spare);

        output.Open(48000, 2, _ => { });

        Assert.True(spare.IsOpen);
        Assert.IsType<InvalidOperationException>(output.Failure);
        Assert.Equal(48000, output.MixSampleRate);

        output.Close();
        Assert.False(spare.IsOpen);
        Assert.False(output.HasDevice);
    }

    [Fact]
    public void TheSilentOutputCallsBackInTimeWithoutADevice()
    {
        using var output = new SilentOutput();
        var blocks = 0;
        var frames = 0;
        var clock = Stopwatch.StartNew();

        output.Open(48000, 2, buffer =>
        {
            frames = buffer.Length / 2;
            Interlocked.Increment(ref blocks);
        });
        // Wait for blocks instead of a fixed time: a busy CI runner may start the thread late.
        var called = SpinWait.SpinUntil(() => Volatile.Read(ref blocks) >= 5, TimeSpan.FromSeconds(5));
        output.Close();
        var elapsed = clock.Elapsed;
        var afterClose = Volatile.Read(ref blocks);
        Thread.Sleep(30);

        Assert.True(called);
        Assert.False(output.HasDevice);
        Assert.Equal(480, frames);
        // Block n runs no earlier than (n - 1) periods after the start, so it never runs ahead of the clock.
        Assert.InRange(afterClose, 5, (int)(elapsed / output.Period) + 1);
        Assert.Equal(afterClose, Volatile.Read(ref blocks));
    }

    private sealed class BrokenOutput : IAudioOutput
    {
        public string? DeviceName => null;

        public bool HasDevice => false;

        public TimeSpan Period => TimeSpan.FromMilliseconds(10);

        public int MixSampleRate => throw new InvalidOperationException("no device");

        public void Open(int sampleRate, int channels, AudioCallback callback) => throw new InvalidOperationException("no device");

        public void Close()
        {
        }

        public void Dispose()
        {
        }
    }
}
