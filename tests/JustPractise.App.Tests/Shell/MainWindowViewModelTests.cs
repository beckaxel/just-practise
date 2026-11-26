using System.Globalization;
using JustPractise.App.FileBrowser;
using JustPractise.App.Localization;
using JustPractise.App.Metronome;
using JustPractise.App.Player;
using JustPractise.App.Shell;
using JustPractise.App.Tests.Updates;
using JustPractise.App.Timer;
using JustPractise.App.Tuner;
using JustPractise.Audio.Tuner;
using JustPractise.App.Updates;
using JustPractise.App.Shortcuts;
using JustPractise.App.Tests.Metronome;
using JustPractise.App.Tests.Player;
using JustPractise.Audio;
using JustPractise.Core.DataFiles;
using JustPractise.Core.Settings;
using JustPractise.Storage;

using JustPractise.App.Tests.FileBrowser;

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
        var browser = new FileBrowserViewModel(_memory, new NoPicker(), new FakeRemoveFolderDialog());
        var developer = new DeveloperMode(_memory);
        _shell = new MainWindowViewModel(
            browser,
            new PlayerViewModel(_player),
            new MetronomeViewModel(_metronome, _settings),
            new TimerViewModel(TimeProvider.System, _settings),
            new TunerViewModel(new PitchTracker(new NoInput(), TimeProvider.System), _settings, _memory, translator, TimeProvider.System),
            new StatusBarViewModel(new NoDevice(), translator, developer),
            new SettingsViewModel(translator, _settings, browser, new UpdatesViewModel(new FakeUpdateService(), translator), developer),
            new LibraryPane(_memory),
            new AboutViewModel(translator));
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

        await _shell.Browser.ActivateCommand.ExecuteAsync(_shell.Browser.Rows.Single(row => row.IsFile));
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

    [Fact]
    public async Task APathCrumbOpensTheCollapsedLibrary()
    {
        _directory.Create(Path.Combine("Musik", "a.mp3"));
        _memory.Update(memory => memory with { MusicFolders = [_directory.File("Musik")], LastFolder = _directory.File("Musik") });
        await _shell.Browser.StartAsync();
        _shell.LibraryPane.ToggleCommand.Execute(null);
        Assert.True(_shell.LibraryPane.IsCollapsed);

        await _shell.Browser.OpenBreadcrumbCommand.ExecuteAsync(_shell.Browser.Breadcrumbs[0]);

        Assert.False(_shell.LibraryPane.IsCollapsed);
    }

    [Fact]
    public void OpensOneToolAtATimeAndClosesThemAll()
    {
        _shell.OpenMetronomeCommand.Execute(null);
        _shell.OpenTimerCommand.Execute(null);
        Assert.False(_shell.IsMetronomeOpen);
        Assert.True(_shell.IsTimerOpen);

        Assert.True(_shell.CloseTools());
        Assert.False(_shell.IsTimerOpen);
        Assert.False(_shell.CloseTools());
    }

    [Fact]
    public void TimerShortcutsStartResetAndChangeTheDuration()
    {
        Assert.Equal(ShortcutAction.TimerStartPause, Shortcut.Match(Avalonia.Input.Key.T, Avalonia.Input.KeyModifiers.None, "t"));
        Assert.Equal(ShortcutAction.TimerReset, Shortcut.Match(Avalonia.Input.Key.T, Avalonia.Input.KeyModifiers.Shift, "T"));
        Assert.Equal(ShortcutAction.TimerLonger, Shortcut.Match(Avalonia.Input.Key.Up, Avalonia.Input.KeyModifiers.Shift, null));
        Assert.Equal(ShortcutAction.TimerShorter, Shortcut.Match(Avalonia.Input.Key.Down, Avalonia.Input.KeyModifiers.Shift, null));

        _shell.Execute(ShortcutAction.TimerLonger);
        Assert.Equal(11, _shell.Timer.CurrentMinutes);
        _shell.Execute(ShortcutAction.TimerStartPause);
        Assert.True(_shell.Timer.IsRunning);
        _shell.Execute(ShortcutAction.TimerReset);
        Assert.False(_shell.Timer.IsRunning);
        Assert.True(_shell.Timer.IsReady);
    }

    [Fact]
    public void STogglesTheTunerAndOnlyOneToolIsOpen()
    {
        Assert.Equal(ShortcutAction.TunerToggle, Shortcut.Match(Avalonia.Input.Key.S, Avalonia.Input.KeyModifiers.None, "s"));

        _shell.Execute(ShortcutAction.TunerToggle);
        Assert.True(_shell.Tuner.IsUnavailable); // im Test gibt es kein Mikrofon

        _shell.OpenTimerCommand.Execute(null);
        _shell.OpenTunerCommand.Execute(null);
        Assert.False(_shell.IsTimerOpen);
        Assert.True(_shell.IsTunerOpen);
    }

    [Fact]
    public void AsksTheWindowToShowAbout()
    {
        var requests = 0;
        _shell.AboutRequested += (_, _) => requests++;
        _shell.Settings.AboutRequested += (_, _) => requests++;

        _shell.OpenAboutCommand.Execute(null);
        _shell.Settings.OpenAboutCommand.Execute(null);

        Assert.Equal(2, requests);
    }

    [Fact]
    public void OpensAndClosesTheMetronomeSettings()
    {
        _shell.OpenMetronomeCommand.Execute(null);
        Assert.True(_shell.IsMetronomeOpen);

        _shell.CloseMetronomeCommand.Execute(null);
        Assert.False(_shell.IsMetronomeOpen);
    }

    [Fact]
    public void F11AsksTheWindowToToggleFullScreen()
    {
        var requests = 0;
        _shell.FullScreenToggleRequested += (_, _) => requests++;

        _shell.Execute(ShortcutAction.ToggleFullScreen);

        Assert.Equal(1, requests);
        Assert.Equal(ShortcutAction.ToggleFullScreen, Shortcut.Match(Avalonia.Input.Key.F11, Avalonia.Input.KeyModifiers.None, null));
    }

    private sealed class NoInput : IAudioInput
    {
        public int SampleRate => 48000;

        public IReadOnlyList<string> DeviceNames() => [];

        public bool Open(string? deviceName, InputRing ring) => false;

        public void Close()
        {
        }
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
