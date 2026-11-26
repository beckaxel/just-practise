using JustPractise.Core.DataFiles;
using JustPractise.Core.Settings;
using JustPractise.Core.Timer;

namespace JustPractise.Core.Tests.Timer;

public sealed class PracticeTimerTests
{
    private readonly ManualTime _time = new();

    [Fact]
    public void StartsReadyWithTheFullDuration()
    {
        var timer = new PracticeTimer(_time, 25);

        Assert.Equal(TimerPhase.Ready, timer.Phase);
        Assert.Equal("25", timer.Display);
        Assert.False(timer.DisplaysSeconds);
        Assert.Equal(1, timer.Fraction);
    }

    [Fact]
    public void RoundsMinutesUpSoTheStartValueStaysAWhile()
    {
        var timer = new PracticeTimer(_time, 25);
        timer.Start();

        _time.Advance(TimeSpan.FromSeconds(59));
        Assert.Equal("25", timer.Display);
        Assert.Equal(TimerPhase.Running, timer.Phase);

        _time.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal("24", timer.Display);
    }

    [Theory]
    [InlineData(31, "1", false)]
    [InlineData(30, "30", true)]
    [InlineData(21, "30", true)]
    [InlineData(20, "20", true)]
    [InlineData(1, "10", true)]
    public void CountsTheLastThirtySecondsInStepsOfTen(int secondsLeft, string display, bool seconds)
    {
        var timer = new PracticeTimer(_time, 1);
        timer.Start();

        _time.Advance(TimeSpan.FromSeconds(60 - secondsLeft));

        Assert.Equal(display, timer.Display);
        Assert.Equal(seconds, timer.DisplaysSeconds);
        Assert.Equal(seconds ? TimerPhase.LastSeconds : TimerPhase.Running, timer.Phase);
    }

    [Fact]
    public void StopsAtZeroLikeAnHourglassAndStaysUntilReset()
    {
        var timer = new PracticeTimer(_time, 1);
        timer.Start();

        _time.Advance(TimeSpan.FromMinutes(5));

        Assert.Equal(TimerPhase.Finished, timer.Phase);
        Assert.Equal("0", timer.Display);
        Assert.Equal(0, timer.Fraction);
        Assert.False(timer.IsRunning);

        _time.Advance(TimeSpan.FromMinutes(10));
        Assert.Equal(TimerPhase.Finished, timer.Phase);

        timer.Start(); // Neustart mit den zuletzt gesetzten Minuten
        Assert.Equal(TimerPhase.Running, timer.Phase);
        Assert.Equal("1", timer.Display);
        Assert.Equal(1, timer.Fraction);
    }

    [Fact]
    public void PausesAndContinuesWhereItStopped()
    {
        var timer = new PracticeTimer(_time, 10);
        timer.Start();
        _time.Advance(TimeSpan.FromMinutes(3));

        timer.StartOrPause();
        _time.Advance(TimeSpan.FromMinutes(20));
        Assert.Equal(TimerPhase.Paused, timer.Phase);
        Assert.Equal("7", timer.Display);
        Assert.Equal(0.7, timer.Fraction, 3);

        timer.StartOrPause();
        _time.Advance(TimeSpan.FromMinutes(2));
        Assert.Equal("5", timer.Display);
        Assert.True(timer.IsRunning);
    }

    [Fact]
    public void SettingMinutesWhileRunningContinuesFromThere()
    {
        var timer = new PracticeTimer(_time, 10);
        timer.Start();
        _time.Advance(TimeSpan.FromMinutes(5));
        Assert.Equal(5, timer.DisplayMinutes);

        timer.SetMinutes(20);
        Assert.Equal("20", timer.Display);
        Assert.Equal(1, timer.Fraction);
        Assert.True(timer.IsRunning);

        _time.Advance(TimeSpan.FromMinutes(2));
        Assert.Equal("18", timer.Display);

        timer.SetMinutes(3); // verkürzen geht genauso
        _time.Advance(TimeSpan.FromMinutes(1));
        Assert.Equal("2", timer.Display);
    }

    [Fact]
    public void SettingMinutesKeepsAPausedTimerPausedAndMakesAFinishedOneReady()
    {
        var timer = new PracticeTimer(_time, 10);
        timer.Start();
        _time.Advance(TimeSpan.FromMinutes(3));
        timer.Pause();

        timer.SetMinutes(15);
        _time.Advance(TimeSpan.FromMinutes(5));
        Assert.Equal(TimerPhase.Paused, timer.Phase);
        Assert.Equal("15", timer.Display);

        timer.Start();
        _time.Advance(TimeSpan.FromMinutes(20));
        Assert.Equal(TimerPhase.Finished, timer.Phase);
        timer.SetMinutes(5);
        Assert.Equal(TimerPhase.Ready, timer.Phase);
        Assert.Equal("5", timer.Display);
    }

    [Fact]
    public void StoresOnlyADurationThatDiffersFromTheDefault()
    {
        Assert.True(TimerSettings.Default.WithMinutes(10).IsDefault);
        Assert.Equal(25, TimerSettings.Default.WithMinutes(25).EffectiveMinutes);
        Assert.Equal(60, TimerSettings.Default.WithMinutes(90).EffectiveMinutes);

        var json = """{ "format": 1, "timer": { "minutes": 25 } }""";
        Assert.Equal(25, AppDataFiles.Settings.Read(json).Value.Timer.EffectiveMinutes);
    }

    /// <summary>Eine Uhr, die nur auf Anweisung weiterläuft.</summary>
    private sealed class ManualTime : TimeProvider
    {
        private long _ticks;

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override long GetTimestamp() => _ticks;

        public void Advance(TimeSpan by) => _ticks += by.Ticks;
    }
}
