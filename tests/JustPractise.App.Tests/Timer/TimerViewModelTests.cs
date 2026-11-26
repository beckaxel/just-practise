using JustPractise.App.Timer;
using JustPractise.Core.DataFiles;
using JustPractise.Core.Settings;
using JustPractise.Core.Timer;
using JustPractise.Storage;

namespace JustPractise.App.Tests.Timer;

public sealed class TimerViewModelTests : IAsyncDisposable
{
    private readonly TemporaryDirectory _directory = new();
    private readonly ManualTime _time = new();
    private readonly List<DataFileKeeper<AppSettings>> _keepers = [];

    public async ValueTask DisposeAsync()
    {
        foreach (var keeper in _keepers)
        {
            await keeper.FlushAsync();
        }

        _directory.Dispose();
    }

    private DataFileKeeper<AppSettings> Settings()
    {
        var keeper = new DataFileKeeper<AppSettings>(new DataFileStore<AppSettings>(_directory.File("settings.json"), AppDataFiles.Settings));
        _keepers.Add(keeper);
        return keeper;
    }

    [Fact]
    public async Task StartsWithTenMinutesWithoutWritingSettings()
    {
        var settings = Settings();
        var timer = new TimerViewModel(_time, settings);
        await settings.FlushAsync();

        Assert.Equal(10, timer.Minutes);
        Assert.Equal(10, timer.CurrentMinutes);
        Assert.Equal("10 min", timer.DisplayText);
        Assert.Equal(1, timer.Fraction);
        Assert.True(timer.IsReady);
        Assert.True(timer.Presets.Single(preset => preset.Value == 10).IsSelected);
        Assert.False(File.Exists(_directory.File("settings.json")));
    }

    [Fact]
    public async Task RemembersTheDuration()
    {
        var settings = Settings();
        var timer = new TimerViewModel(_time, settings);

        timer.UsePresetCommand.Execute(timer.Presets.Single(preset => preset.Value == 15));
        await settings.FlushAsync();

        Assert.Equal("15 min", timer.DisplayText);
        Assert.Equal(15, new TimerViewModel(_time, Settings()).Minutes);
        Assert.Equal([5, 10, 15, 20, 30], timer.Presets.Select(preset => preset.Value));
    }

    [Fact]
    public void ShowsThePhaseAndEmptiesTheBarLikeAnHourglass()
    {
        var timer = new TimerViewModel(_time, Settings()) { CurrentMinutes = 1 };

        timer.StartPauseCommand.Execute(null);
        _time.Advance(TimeSpan.FromSeconds(15));
        timer.Tick();
        Assert.True(timer.IsRunningPhase);
        Assert.Equal(0.75, timer.Fraction, 3);
        Assert.Equal("1 min", timer.DisplayText);

        _time.Advance(TimeSpan.FromSeconds(20));
        timer.Tick();
        Assert.True(timer.IsLastSeconds);
        Assert.Equal("30 s", timer.DisplayText);

        timer.StartPauseCommand.Execute(null);
        Assert.True(timer.IsPaused);
        Assert.False(timer.IsRunning);

        timer.StartPauseCommand.Execute(null);
        _time.Advance(TimeSpan.FromMinutes(1));
        timer.Tick();
        Assert.True(timer.IsFinished);
        Assert.Equal("0 min", timer.DisplayText);
        Assert.Equal(1, timer.Fraction); // abgelaufen: voller Balken in der Farbe „geschafft“

        Assert.True(timer.IsFinished);
        Assert.False(timer.ShowsPlay);
        timer.StartPauseCommand.Execute(null); // Neustart mit der eingestellten Dauer
        Assert.True(timer.IsRunning);
        Assert.Equal("1 min", timer.DisplayText);
        Assert.Equal(1, timer.Fraction);
    }

    [Fact]
    public void KeepsTheDurationWithinOneToSixtyMinutes()
    {
        var timer = new TimerViewModel(_time, Settings()) { CurrentMinutes = 59 };

        timer.ChangeMinutes(5);
        Assert.Equal(60, timer.CurrentMinutes);

        timer.CurrentMinutes = 0;
        Assert.Equal(1, timer.CurrentMinutes);
    }

    [Fact]
    public void TheSliderFollowsTheRemainingMinutesAndAChangeAppliesAtOnce()
    {
        var timer = new TimerViewModel(_time, Settings()) { CurrentMinutes = 10 };
        timer.StartPauseCommand.Execute(null);
        _time.Advance(TimeSpan.FromMinutes(3));
        timer.Tick();
        Assert.Equal(7, timer.CurrentMinutes);
        Assert.Equal(10, timer.Minutes);

        timer.CurrentMinutes = 7; // nur zurückgespiegelt: nichts ändert sich
        _time.Advance(TimeSpan.FromSeconds(30));
        timer.Tick();
        Assert.Equal(0.65, timer.Fraction, 3);

        timer.CurrentMinutes = 20; // verlängern, läuft von dort weiter
        Assert.True(timer.IsRunning);
        Assert.Equal("20 min", timer.DisplayText);
        Assert.Equal(1, timer.Fraction);
        Assert.Equal(20, timer.Minutes);
        Assert.True(timer.Presets.Single(preset => preset.Value == 20).IsSelected);

        _time.Advance(TimeSpan.FromMinutes(5));
        timer.Tick();
        Assert.Equal(15, timer.CurrentMinutes);
        timer.ChangeMinutes(-10); // verkürzen per Umschalt+↓
        Assert.Equal("5 min", timer.DisplayText);
        Assert.True(timer.IsRunning);
    }

    [Fact]
    public void AChangeAfterTheEndMakesTheTimerReady()
    {
        var timer = new TimerViewModel(_time, Settings()) { CurrentMinutes = 1 };
        timer.StartPauseCommand.Execute(null);
        _time.Advance(TimeSpan.FromMinutes(2));
        timer.Tick();
        Assert.True(timer.IsFinished);

        timer.UsePresetCommand.Execute(timer.Presets[0]);

        Assert.True(timer.IsReady);
        Assert.Equal("5 min", timer.DisplayText);
    }

    private sealed class ManualTime : TimeProvider
    {
        private long _ticks;

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override long GetTimestamp() => _ticks;

        public void Advance(TimeSpan by) => _ticks += by.Ticks;
    }
}
