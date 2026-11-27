using JustPractise.App.FileBrowser;
using JustPractise.App.Player;
using JustPractise.Audio;
using JustPractise.Core.Pieces;
using JustPractise.Core.Waveform;
using JustPractise.Storage;

namespace JustPractise.App.Tests.Player;

public sealed class PlayerViewModelTests : IAsyncDisposable
{
    private readonly TemporaryDirectory _directory = new();
    private readonly FakePlayer _player = new();
    private readonly PlayerViewModel _viewModel;

    public PlayerViewModelTests() => _viewModel = new PlayerViewModel(_player);

    /// <summary>Erst warten, bis die Sidecar-Datei im Hintergrund geschrieben ist, dann den Ordner löschen.</summary>
    public async ValueTask DisposeAsync()
    {
        await _viewModel.FlushAsync();
        _directory.Dispose();
    }

    private PieceFile File(string fileName = "Blues - Playalong.mp3", string piece = "Blues") => new(_directory.Path, piece, fileName);

    private string SidecarPath => _directory.File("Blues.jps");

    private void WriteSidecar(string fileName, FileSettings settings)
    {
        var store = AppDataLocation.Sidecar(_directory.Path, "Blues");
        store.Load();
        store.TrySave(new SidecarFile().WithSettings(fileName, settings));
    }

    private FileSettings ReadSettings(string fileName) =>
        AppDataLocation.Sidecar(_directory.Path, "Blues").Load().Value.SettingsFor(fileName);

    [Fact]
    public async Task LoadsTheFileWithTempoAndVolumeFromTheSidecarWithoutWriting()
    {
        WriteSidecar("Blues - Playalong.mp3", FileSettings.Default.WithTempo(0.8).WithVolumeDb(-3));
        var written = System.IO.File.GetLastWriteTimeUtc(SidecarPath);

        await _viewModel.OpenAsync(File());
        await _viewModel.FlushAsync();

        Assert.True(_viewModel.IsReady);
        Assert.Equal(80, _viewModel.TempoPercent);
        Assert.Equal(-3, _viewModel.VolumeDb);
        Assert.Equal(0.8, _player.Tempo);
        Assert.Equal(-3, _player.VolumeDb);
        Assert.Equal("3:00", _viewModel.DurationText);
        Assert.Equal(written, System.IO.File.GetLastWriteTimeUtc(SidecarPath));
    }

    [Fact]
    public async Task UsesDefaultsForAFileWithoutSidecarAndDoesNotCreateOne()
    {
        WriteSidecar("Blues - Demo.mp3", FileSettings.Default.WithTempo(0.7));
        await _viewModel.OpenAsync(File("Blues - Demo.mp3"));

        await _viewModel.OpenAsync(File("Blues - Playalong.mp3"));
        await _viewModel.FlushAsync();

        Assert.Equal(100, _viewModel.TempoPercent);
        Assert.Equal(1.0, _player.Tempo);

        await _viewModel.OpenAsync(new PieceFile(_directory.Path, "Telstar", "Telstar.mp3"));
        await _viewModel.FlushAsync();
        Assert.False(System.IO.File.Exists(_directory.File("Telstar.jps")));
    }

    [Fact]
    public async Task SavesTempoAndVolumePerFile()
    {
        await _viewModel.OpenAsync(File());

        _viewModel.TempoPercent = 75;
        _viewModel.VolumeDb = 2.5;
        await _viewModel.FlushAsync();

        Assert.Equal(0.75, _player.Tempo);
        Assert.Equal(2.5, _player.VolumeDb);
        var settings = ReadSettings("Blues - Playalong.mp3");
        Assert.Equal(0.75, settings.Tempo);
        Assert.Equal(2.5, settings.VolumeDb);
        Assert.True(ReadSettings("Blues - Demo.mp3").IsDefault);
    }

    [Fact]
    public async Task ResettingRemovesTheValuesFromTheSidecar()
    {
        WriteSidecar("Blues - Playalong.mp3", FileSettings.Default.WithTempo(0.8).WithVolumeDb(-3));
        await _viewModel.OpenAsync(File());

        _viewModel.ResetTempoCommand.Execute(null);
        _viewModel.ResetVolumeCommand.Execute(null);
        await _viewModel.FlushAsync();

        Assert.Equal(1.0, _player.Tempo);
        Assert.Equal(0, _player.VolumeDb);
        Assert.True(ReadSettings("Blues - Playalong.mp3").IsDefault);
    }

    [Fact]
    public async Task ShowsOnlyTheFileChosenLast()
    {
        var first = File("Blues - Demo.mp3");
        var second = File("Blues - Playalong.mp3");
        _player.Hold(first.Path);

        var loadingFirst = _viewModel.OpenAsync(first);
        await _viewModel.OpenAsync(second);
        _player.Release(first.Path);
        await loadingFirst;

        Assert.Equal(second, _viewModel.File);
        Assert.True(_viewModel.IsReady);
    }

    [Fact]
    public async Task ReportsAFileThatCannotBeOpened()
    {
        _player.LoadFailure = new InvalidDataException("not audio");

        await _viewModel.OpenAsync(File());

        Assert.True(_viewModel.HasError);
        Assert.False(_viewModel.IsLoading);
        Assert.False(_viewModel.IsReady);
        _viewModel.PlayPauseCommand.Execute(null);
        Assert.DoesNotContain("play", _player.Calls);
    }

    [Fact]
    public async Task PlaysPausesAndStartsAgainFromTheBeginningAtTheEnd()
    {
        await _viewModel.OpenAsync(File());

        _viewModel.PlayPauseCommand.Execute(null);
        Assert.True(_viewModel.IsPlaying);
        _viewModel.PlayPauseCommand.Execute(null);
        Assert.False(_viewModel.IsPlaying);

        _player.State = _player.State with { Position = TimeSpan.FromMinutes(3), IsPlaying = false };
        _viewModel.Tick();
        _player.Calls.Clear();
        _viewModel.PlayPauseCommand.Execute(null);

        Assert.Equal(["start", "play"], _player.Calls);
    }

    [Fact]
    public async Task ShowsThePositionWithoutSeekingAndSeeksWhenTheUserMovesIt()
    {
        await _viewModel.OpenAsync(File());

        _player.State = _player.State with { Position = TimeSpan.FromSeconds(65), IsPlaying = true };
        _viewModel.Tick();

        Assert.Equal(65, _viewModel.PositionSeconds);
        Assert.Equal("1:05", _viewModel.PositionText);
        Assert.True(_viewModel.IsPlaying);
        Assert.DoesNotContain(_player.Calls, call => call.StartsWith("seek", StringComparison.Ordinal));

        _viewModel.PositionSeconds = 90;

        Assert.Contains("seek 90", _player.Calls);
        Assert.Equal("1:30", _viewModel.PositionText);
    }

    [Fact]
    public async Task GoesBackToTheStart()
    {
        await _viewModel.OpenAsync(File());
        _player.State = _player.State with { Position = TimeSpan.FromSeconds(30) };
        _viewModel.Tick();

        _viewModel.ToStartCommand.Execute(null);

        Assert.Contains("start", _player.Calls);
        Assert.Equal(0, _viewModel.PositionSeconds);
    }

    [Fact]
    public async Task ClosesTheFileAndWritesNothingAfterwards()
    {
        await _viewModel.OpenAsync(File());
        _viewModel.TempoPercent = 80;

        await _viewModel.CloseAsync();
        _viewModel.TempoPercent = 90;
        await _viewModel.FlushAsync();

        Assert.False(_viewModel.HasFile);
        Assert.Contains("unload", _player.Calls);
        Assert.Contains("\"tempo\": 0.8", System.IO.File.ReadAllText(SidecarPath), StringComparison.Ordinal);
    }

    [Fact]
    public async Task StepsThroughTheFileTempoAndVolumeWithinTheirLimits()
    {
        await _viewModel.OpenAsync(File());

        _viewModel.SeekBy(-5);
        Assert.Equal(0, _viewModel.PositionSeconds);
        _viewModel.SeekBy(5);
        Assert.Contains("seek 5", _player.Calls);
        _viewModel.SeekBy(1000);
        Assert.Equal(180, _viewModel.PositionSeconds);

        for (var i = 0; i < 20; i++)
        {
            _viewModel.ChangeTempo(-5);
            _viewModel.ChangeVolume(1);
        }

        Assert.Equal(50, _viewModel.TempoPercent);
        Assert.Equal(0.5, _player.Tempo);
        Assert.Equal(10, _viewModel.VolumeDb);
        _viewModel.ChangeTempo(5);
        Assert.Equal(55, _viewModel.TempoPercent);
    }

    [Fact]
    public async Task ShowsTheWholePieceAndTheWaveformOnceItIsReady()
    {
        await _viewModel.OpenAsync(File());
        Assert.Null(_viewModel.Waveform);
        Assert.Equal(WaveformViewport.Whole(180), _viewModel.Viewport);

        var waveform = new WaveformPeaksBuilder(1, 48000, 48000).Build();
        _player.State = _player.State with { Waveform = waveform };
        _viewModel.Tick();

        Assert.Same(waveform, _viewModel.Waveform);
    }

    [Fact]
    public async Task ZoomsAroundThePositionAndBackToTheWholePiece()
    {
        await _viewModel.OpenAsync(File());
        _player.State = _player.State with { Position = TimeSpan.FromSeconds(90) };
        _viewModel.Tick();

        _viewModel.ZoomIn();
        Assert.Equal(WaveformViewport.Of(180, 45, 90), _viewModel.Viewport);
        _viewModel.ZoomIn();
        Assert.Equal(WaveformViewport.Of(180, 67.5, 45), _viewModel.Viewport);
        _viewModel.ZoomOut();
        Assert.Equal(WaveformViewport.Of(180, 45, 90), _viewModel.Viewport);
        _viewModel.ZoomAll();
        Assert.True(_viewModel.Viewport.IsWhole);
    }

    [Fact]
    public async Task TurnsThePageWhilePlaying()
    {
        await _viewModel.OpenAsync(File());
        _viewModel.ZoomIn();
        _viewModel.ZoomIn(); // 0 bis 45 s
        _viewModel.PlayPause();

        _player.State = _player.State with { Position = TimeSpan.FromSeconds(46) };
        _viewModel.Tick();

        Assert.Equal(WaveformViewport.Of(180, 45, 45), _viewModel.Viewport);
    }

    [Fact]
    public async Task FollowsAgainOnlyWhenThePositionReachesAViewportMovedAway()
    {
        await _viewModel.OpenAsync(File());
        _viewModel.ZoomIn();
        _viewModel.ZoomIn();
        _viewModel.PlayPause();

        _viewModel.Viewport = WaveformViewport.Of(180, 60, 45); // selbst weggeschoben
        _player.State = _player.State with { Position = TimeSpan.FromSeconds(50) };
        _viewModel.Tick();
        Assert.Equal(60, _viewModel.Viewport.Start);

        _player.State = _player.State with { Position = TimeSpan.FromSeconds(61) };
        _viewModel.Tick();
        _player.State = _player.State with { Position = TimeSpan.FromSeconds(106) };
        _viewModel.Tick();
        Assert.Equal(105, _viewModel.Viewport.Start);
    }

    [Fact]
    public async Task JumpingWithTheKeysBringsThePositionBackIntoView()
    {
        await _viewModel.OpenAsync(File());
        _viewModel.ZoomIn();
        _viewModel.ZoomIn();
        _viewModel.Viewport = WaveformViewport.Of(180, 100, 45);

        _viewModel.SeekBy(PlayerViewModel.SeekSeconds);

        Assert.True(_viewModel.Viewport.Contains(5));
        _viewModel.Viewport = WaveformViewport.Of(180, 100, 45);
        _viewModel.ToStart();
        Assert.Equal(0, _viewModel.Viewport.Start);
    }

    [Fact]
    public void IgnoresStepsBeforeAFileIsLoaded()
    {
        _viewModel.SeekBy(5);
        _viewModel.ChangeTempo(5);
        _viewModel.ChangeVolume(1);

        Assert.Equal(100, _viewModel.TempoPercent);
        Assert.Equal(0, _viewModel.VolumeDb);
        Assert.Empty(_player.Calls);
    }

    [Theory]
    [InlineData(0, "0:00")]
    [InlineData(65.4, "1:05")]
    [InlineData(600, "10:00")]
    [InlineData(3725, "1:02:05")]
    public void FormatsTimes(double seconds, string expected) =>
        Assert.Equal(expected, PlayerViewModel.FormatTime(TimeSpan.FromSeconds(seconds)));

    [Fact]
    public void ShowsNothingBeforeAFileIsChosen()
    {
        _viewModel.Tick();
        _viewModel.PlayPauseCommand.Execute(null);

        Assert.False(_viewModel.HasFile);
        Assert.Empty(_player.Calls);
        Assert.Equal(default(PlayerState), _player.State);
    }
}
