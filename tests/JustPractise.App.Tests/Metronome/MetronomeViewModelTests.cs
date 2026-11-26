using JustPractise.App.Metronome;
using JustPractise.Audio.Graph;
using JustPractise.Core.DataFiles;
using JustPractise.Core.Metronome;
using JustPractise.Core.Settings;
using JustPractise.Storage;

namespace JustPractise.App.Tests.Metronome;

public sealed class MetronomeViewModelTests : IAsyncDisposable
{
    private readonly TemporaryDirectory _directory = new();
    private readonly FakeMetronome _metronome = new();
    private readonly List<DataFileKeeper<AppSettings>> _settings = [];

    /// <summary>Erst warten, bis die Einstellungen im Hintergrund geschrieben sind, dann den Ordner löschen.</summary>
    public async ValueTask DisposeAsync()
    {
        foreach (var settings in _settings)
        {
            await settings.FlushAsync();
        }

        _directory.Dispose();
    }

    private DataFileStore<AppSettings> Store() => new(_directory.File("settings.json"), AppDataFiles.Settings);

    private DataFileKeeper<AppSettings> Settings(AppSettings? value = null)
    {
        if (value is not null)
        {
            var store = Store();
            store.Load();
            store.TrySave(value);
        }

        var settings = new DataFileKeeper<AppSettings>(Store());
        _settings.Add(settings);
        return settings;
    }

    private static AppSettings Saved(int bpm, Meter meter, double volumeDb) => new()
    {
        Metronome = new MetronomeSettings { Current = MetronomeSetup.Default.WithBpm(bpm).WithMeter(meter) }.WithVolumeDb(volumeDb),
    };

    [Fact]
    public async Task StartsWithDefaultsWithoutWritingSettings()
    {
        var settings = Settings();
        var viewModel = new MetronomeViewModel(_metronome, settings);
        await settings.FlushAsync();

        Assert.Equal(120, viewModel.Bpm);
        Assert.Equal(Meter.FourFour, viewModel.Meter);
        Assert.True(viewModel.TempoPresets.Single(choice => choice.Value == 120).IsSelected);
        Assert.Equal(0, viewModel.VolumeDb);
        Assert.Equal(4, viewModel.Beats.Count);
        Assert.Equal((120, Meter.FourFour, 0.0), (_metronome.Bpm, _metronome.Meter, _metronome.VolumeDb));
        Assert.False(viewModel.IsRunning);
        Assert.False(File.Exists(Store().Path));
    }

    [Fact]
    public void RestoresTheLastSetting()
    {
        var viewModel = new MetronomeViewModel(_metronome, Settings(Saved(92, Meter.ThreeFour, -4)));

        Assert.Equal(92, viewModel.Bpm);
        Assert.Equal(Meter.ThreeFour, viewModel.Meter);
        Assert.Equal(-4, viewModel.VolumeDb);
        Assert.Equal(3, viewModel.Beats.Count);
        Assert.Equal((92, Meter.ThreeFour, -4.0), (_metronome.Bpm, _metronome.Meter, _metronome.VolumeDb));
    }

    [Fact]
    public async Task SavesTempoMeterAndVolume()
    {
        var settings = Settings();
        var viewModel = new MetronomeViewModel(_metronome, settings);

        viewModel.Bpm = 100;
        viewModel.FewerBeatsCommand.Execute(null);
        viewModel.VolumeDb = -6;
        await settings.FlushAsync();

        Assert.Equal((100, Meter.ThreeFour, -6.0), (_metronome.Bpm, _metronome.Meter, _metronome.VolumeDb));
        var saved = Store().Load().Value.Metronome;
        Assert.Equal(100, saved.Current.EffectiveBpm);
        Assert.Equal(Meter.ThreeFour, saved.Current.EffectiveMeter);
        Assert.Equal(-6, saved.EffectiveVolumeDb);
    }

    [Theory]
    [InlineData(10, 30)]
    [InlineData(400, 250)]
    public void LimitsTheTempo(int input, int expected)
    {
        var viewModel = new MetronomeViewModel(_metronome, Settings());

        viewModel.BpmInput = input;

        Assert.Equal(expected, viewModel.Bpm);
        Assert.Equal(expected, _metronome.Bpm);
    }

    [Fact]
    public void ChangesTheTempoInStepsWithinTheLimits()
    {
        var viewModel = new MetronomeViewModel(_metronome, Settings());

        viewModel.ChangeTempo(10);
        Assert.Equal(130, _metronome.Bpm);
        viewModel.ChangeTempo(-1);
        Assert.Equal(129, viewModel.Bpm);
        viewModel.ChangeTempo(500);
        Assert.Equal(250, viewModel.Bpm);
    }

    [Fact]
    public void KeepsTheTempoWhenTheInputIsCleared()
    {
        var viewModel = new MetronomeViewModel(_metronome, Settings());

        viewModel.BpmInput = null;

        Assert.Equal(120, viewModel.Bpm);
        Assert.Equal(120m, viewModel.BpmInput);
    }

    [Fact]
    public void StartsStopsAndLightsTheCurrentBeat()
    {
        var viewModel = new MetronomeViewModel(_metronome, Settings());

        viewModel.StartStopCommand.Execute(null);
        _metronome.LastBeat = new BeatInfo(48000, 2);
        viewModel.Tick();

        Assert.True(viewModel.IsRunning);
        Assert.Equal([false, false, true, false], viewModel.Beats.Select(beat => beat.IsCurrent));

        viewModel.StartStopCommand.Execute(null);

        Assert.False(viewModel.IsRunning);
        Assert.DoesNotContain(viewModel.Beats, beat => beat.IsCurrent);
    }

    [Fact]
    public void ShowsThreeBeatsWithTheFirstAccentedAfterSwitchingTheMeter()
    {
        var viewModel = new MetronomeViewModel(_metronome, Settings());

        viewModel.FewerBeatsCommand.Execute(null);

        Assert.Equal(Meter.ThreeFour, viewModel.Meter);
        Assert.Equal([true, false, false], viewModel.Beats.Select(beat => beat.IsAccent));
    }

    [Fact]
    public async Task SetsTheBeatsPerBarAndRemembersThem()
    {
        var settings = Settings();
        var viewModel = new MetronomeViewModel(_metronome, settings);

        for (var i = 0; i < 3; i++)
        {
            viewModel.MoreBeatsCommand.Execute(null);
        }

        Assert.Equal(7, viewModel.Meter.Beats);
        Assert.Equal(7, _metronome.Meter.Beats);
        Assert.Equal(7, viewModel.Beats.Count);
        Assert.Equal([true, false, false, false, false, false, false], viewModel.Beats.Select(beat => beat.IsAccent));
        await settings.FlushAsync();
        Assert.Equal(7, new MetronomeViewModel(new FakeMetronome(), Settings()).Meter.Beats);
    }

    [Fact]
    public void TakesOnlyTheBeatsOfASavedMeter()
    {
        var viewModel = new MetronomeViewModel(_metronome, Settings(Saved(120, new Meter(6, 8), 0)));

        Assert.Equal(new Meter(6, 4), viewModel.Meter);
        Assert.Equal(6, viewModel.Beats.Count);
    }

    [Fact]
    public void KeepsTheBeatsPerBarWithinOneToSixteen()
    {
        var viewModel = new MetronomeViewModel(_metronome, Settings(Saved(120, new Meter(2, 4), 0)));

        for (var i = 0; i < 5; i++)
        {
            viewModel.FewerBeatsCommand.Execute(null);
        }

        Assert.Equal(new Meter(1, 4), viewModel.Meter);
        for (var i = 0; i < 30; i++)
        {
            viewModel.MoreBeatsCommand.Execute(null);
        }

        Assert.Equal(new Meter(16, 4), viewModel.Meter);
    }

    [Fact]
    public void ChoosesATempoPreset()
    {
        var viewModel = new MetronomeViewModel(_metronome, Settings());

        viewModel.UseTempoPresetCommand.Execute(viewModel.TempoPresets.Single(choice => choice.Value == 60));

        Assert.Equal(60, viewModel.Bpm);
        Assert.Equal(60, _metronome.Bpm);
        Assert.Equal([60, 90, 120], viewModel.TempoPresets.Select(choice => choice.Value));
        Assert.True(viewModel.TempoPresets[0].IsSelected);
        Assert.False(viewModel.TempoPresets[2].IsSelected);

        viewModel.Bpm = 61;
        Assert.DoesNotContain(viewModel.TempoPresets, choice => choice.IsSelected);
    }

    [Fact]
    public async Task ResetsTheVolume()
    {
        var settings = Settings(Saved(120, Meter.FourFour, -4));
        var viewModel = new MetronomeViewModel(_metronome, settings);

        viewModel.ResetVolumeCommand.Execute(null);
        await settings.FlushAsync();

        Assert.Equal(0, _metronome.VolumeDb);
        Assert.True(Store().Load().Value.Metronome.IsDefault);
    }
}
