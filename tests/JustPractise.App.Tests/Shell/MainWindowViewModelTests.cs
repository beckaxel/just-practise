using System.Globalization;
using JustPractise.App.FileBrowser;
using JustPractise.App.Localization;
using JustPractise.App.Metronome;
using JustPractise.App.Player;
using JustPractise.App.Shell;
using JustPractise.App.Shortcuts;
using JustPractise.App.Tests.Metronome;
using JustPractise.App.Tests.Player;
using JustPractise.Audio;
using JustPractise.Core.DataFiles;
using JustPractise.Core.Settings;
using JustPractise.Storage;

namespace JustPractise.App.Tests.Shell;

public sealed class MainWindowViewModelTests : IAsyncDisposable
{
    private readonly TemporaryDirectory _directory = new();
    private readonly FakePlayer _player = new();
    private readonly FakeMetronome _metronome = new();
    private readonly DataFileKeeper<AppSettings> _settings;
    private readonly DataFileKeeper<DeviceMemory> _memory;
    private readonly MainWindowViewModel _shell;

    public MainWindowViewModelTests()
    {
        _settings = new DataFileKeeper<AppSettings>(new DataFileStore<AppSettings>(_directory.File("settings.json"), AppDataFiles.Settings));
        _memory = new DataFileKeeper<DeviceMemory>(new DataFileStore<DeviceMemory>(_directory.File("device.json"), AppDataFiles.Device));
        var translator = new Translator(TranslationCatalog.Load(null), "en", CultureInfo.GetCultureInfo("en-US"));
        var browser = new FileBrowserViewModel(_memory, new NoPicker());
        _shell = new MainWindowViewModel(
            browser,
            new PlayerViewModel(_player),
            new MetronomeViewModel(_metronome, _settings),
            new StatusBarViewModel(new NoDevice(), translator),
            new SettingsViewModel(translator, _settings, browser));
    }

    public async ValueTask DisposeAsync()
    {
        await _shell.Player.FlushAsync();
        await _settings.FlushAsync();
        await _memory.FlushAsync();
        _directory.Dispose();
    }

    [Fact]
    public async Task LoadsTheFileChosenInTheBrowser()
    {
        _directory.Create(Path.Combine("Musik", "Blues.mp3"));
        _memory.Update(memory => memory with { MusicFolders = [_directory.File("Musik")], LastFolder = _directory.File("Musik") });
        await _shell.Browser.StartAsync();

        _shell.Browser.SelectPieceCommand.Execute(_shell.Browser.Pieces.Single());
        await WaitUntil(() => _shell.Player.IsReady);

        Assert.Contains("load Blues.mp3", _player.Calls);
    }

    [Fact]
    public async Task SendsShortcutsToPlayerAndMetronome()
    {
        await _shell.Player.OpenAsync(new PieceFile(_directory.Path, "Blues", "Blues.mp3"));

        _shell.Execute(ShortcutAction.PlayPause);
        _shell.Execute(ShortcutAction.TempoDown);
        _shell.Execute(ShortcutAction.VolumeDown);
        _shell.Execute(ShortcutAction.SeekForward);
        _shell.Execute(ShortcutAction.MetronomeStartStop);
        _shell.Execute(ShortcutAction.MetronomeMuchFaster);
        _shell.Execute(ShortcutAction.MetronomeSlower);

        Assert.Contains("play", _player.Calls);
        Assert.Contains("seek 5", _player.Calls);
        Assert.Equal(0.95, _player.Tempo);
        Assert.Equal(-1, _player.VolumeDb);
        Assert.True(_metronome.IsRunning);
        Assert.Equal(129, _metronome.Bpm);
    }

    [Fact]
    public void AsksTheWindowToOpenTheSettings()
    {
        var requested = 0;
        _shell.SettingsRequested += (_, _) => requested++;

        _shell.Execute(ShortcutAction.OpenSettings);
        _shell.OpenSettingsCommand.Execute(null);

        Assert.Equal(2, requested);
    }

    private static async Task WaitUntil(Func<bool> condition)
    {
        for (var i = 0; i < 200 && !condition(); i++)
        {
            await Task.Delay(10);
        }

        Assert.True(condition());
    }

    private sealed class NoPicker : IFolderPicker
    {
        public Task<string?> PickFolderAsync() => Task.FromResult<string?>(null);
    }

    private sealed class NoDevice : IAudioDevice
    {
        public bool IsAvailable => false;

        public string? Name => null;

        public long LateCallbacks => 0;
    }
}
