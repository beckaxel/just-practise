using JustPractise.Audio.Graph;
using JustPractise.Audio.Metronome;
using JustPractise.Audio.Player;
using JustPractise.Core.Metronome;
using SoundFlow.Backends.MiniAudio;

namespace JustPractise.Audio.Tests;

// Mit echtem Decoder, aber ohne Gerät: Die Blöcke zieht FakeOutput. Braucht die native Bibliothek jpstretch.
public sealed class AudioSessionTests : IDisposable
{
    private readonly MiniAudioEngine _engine = new();
    private readonly FakeOutput _output = new();
    private readonly AudioSession _session;
    private readonly AudioPlayer _player;
    private readonly AudioMetronome _metronome;
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "jp-tests-" + Guid.NewGuid().ToString("N"));

    public AudioSessionTests()
    {
        Assert.SkipUnless(StretchVoice.IsAvailable(out var reason), reason);
        Directory.CreateDirectory(_directory);
        _session = new AudioSession(_output, new FileDecoder(_engine), TimeProvider.System);
        _player = new AudioPlayer(_session);
        _metronome = new AudioMetronome(_session);
    }

    public void Dispose()
    {
        _session.Dispose();
        _engine.Dispose();
        DeleteDirectory();
    }

    // Ein Test kann enden, während der Decoder seine Datei im Hintergrund noch liest. Windows löscht keine
    // geöffnete Datei, deshalb wird gewartet, bis der Decoder fertig ist.
    private void DeleteDirectory()
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                Directory.Delete(_directory, recursive: true);
                return;
            }
            catch (IOException) when (attempt < 50)
            {
                Thread.Sleep(100);
            }
        }
    }

    private string Wav(string name, int sampleRate, int seconds, int channels = 2) =>
        TestWav.Write(Path.Combine(_directory, name), channels, sampleRate, seconds * sampleRate);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>Wartet, bis die geladene Datei ganz decodiert ist.</summary>
    private async Task WaitUntilDecoded()
    {
        while (!_player.State.IsComplete)
        {
            await Task.Delay(5, Token);
        }
    }

    [Fact]
    public void OpensTheDeviceAt48000WhenStarting()
    {
        Assert.Equal([48000], _output.OpenedRates);
        Assert.Equal("Fake", _session.DeviceName);
        Assert.False(_player.State.IsLoaded);
    }

    [Fact]
    public void OpensTheDeviceAtTheRateOfItsMixer()
    {
        var output = new FakeOutput { MixSampleRate = 44100 };
        using var session = new AudioSession(output, new FileDecoder(_engine), TimeProvider.System);

        Assert.Equal([44100], output.OpenedRates);
        Assert.Equal(44100, session.SampleRate);
    }

    [Fact]
    public async Task LoadsAFileStoppedAtTheBeginning()
    {
        var path = Wav("a.wav", 48000, 2);

        await _player.LoadAsync(path, Token);
        await WaitUntilDecoded();
        _output.Pull(5);

        var state = _player.State;
        Assert.Equal(path, state.Path);
        Assert.False(state.IsPlaying);
        Assert.Equal(TimeSpan.Zero, state.Position);
        Assert.Equal(TimeSpan.FromSeconds(2), state.Duration);
        Assert.Equal([48000], _output.OpenedRates);
    }

    [Fact]
    public async Task PlaysAndReportsThePosition()
    {
        await _player.LoadAsync(Wav("a.wav", 48000, 2), Token);
        _player.Play();

        _output.Pull(50);

        Assert.True(_player.State.IsPlaying);
        Assert.Equal(TimeSpan.FromSeconds(0.5), _player.State.Position);
    }

    [Fact]
    public async Task KeepsTheDeviceOpenForAFileWithAnotherRate()
    {
        _metronome.SetTempo(90);
        _metronome.SetRunning(true);
        _output.Pull(1);

        await _player.LoadAsync(Wav("cd.wav", 44100, 1), Token);
        _output.Pull(1);

        Assert.Equal([48000], _output.OpenedRates);
        Assert.Equal(48000, _session.SampleRate);
        Assert.True(_metronome.IsRunning);
        Assert.Equal(90, _metronome.Bpm);
        Assert.Equal(0, _metronome.LastBeat?.IndexInBar);
    }

    [Fact]
    public async Task PlaysAFileWithAnotherRateAtItsOwnSpeed()
    {
        _player.SetTempo(0.5);
        await _player.LoadAsync(Wav("cd.wav", 44100, 2), Token);
        _player.Play();

        _output.Pull(100);

        Assert.Equal(TimeSpan.FromSeconds(0.5), _player.State.Position);
    }

    [Fact]
    public async Task StopsAtTheEnd()
    {
        await _player.LoadAsync(Wav("short.wav", 48000, 1), Token);
        await WaitUntilDecoded();
        _player.Play();

        _output.Pull(120);

        Assert.True(_player.State.IsAtEnd);
    }

    [Fact]
    public async Task SeeksAndGoesBackToTheStart()
    {
        await _player.LoadAsync(Wav("a.wav", 48000, 3), Token);
        await WaitUntilDecoded();

        _player.Seek(TimeSpan.FromSeconds(2));
        _output.Pull(1);
        Assert.Equal(TimeSpan.FromSeconds(2), _player.State.Position);

        _player.ToStart();
        _output.Pull(1);
        Assert.Equal(TimeSpan.Zero, _player.State.Position);
    }

    [Fact]
    public async Task ReleasesTheBlocksOfAReplacedFile()
    {
        await _player.LoadAsync(Wav("a.wav", 48000, 10), Token);
        await WaitUntilDecoded();
        await _player.LoadAsync(Wav("b.wav", 48000, 1), Token);
        await WaitUntilDecoded();

        _output.Pull(1);
        _session.ReleaseRetired();
        while (_session.Pool.FreeBlocks == 0)
        {
            await Task.Delay(5, Token);
        }

        Assert.Equal("b.wav", Path.GetFileName(_player.State.Path));
        Assert.InRange(_session.Pool.FreeBlocks, 1, 1); // so viele, wie die aktuelle Datei braucht
    }

    [Fact]
    public async Task KeepsTheCurrentFileWhenLoadingFails()
    {
        var path = Wav("a.wav", 48000, 1);
        await _player.LoadAsync(path, Token);

        await Assert.ThrowsAsync<FileNotFoundException>(() => _player.LoadAsync(Path.Combine(_directory, "missing.mp3"), Token));
        var notAudio = Path.Combine(_directory, "notes.mp3");
        await File.WriteAllTextAsync(notAudio, "kein Audio", Token);
        await Assert.ThrowsAsync<InvalidDataException>(() => _player.LoadAsync(notAudio, Token));

        Assert.Equal(path, _player.State.Path);
    }

    [Fact]
    public async Task UnloadsTheFile()
    {
        await _player.LoadAsync(Wav("a.wav", 48000, 1), Token);
        _player.Play();
        _output.Pull(1);

        _player.Unload();
        var block = _output.Pull(1);

        Assert.False(_player.State.IsLoaded);
        Assert.All(block, sample => Assert.Equal(0f, sample));
    }

    [Fact]
    public void RunsTheMetronomeWithoutAFile()
    {
        _metronome.SetMeter(Meter.ThreeFour);
        _metronome.SetRunning(true);

        var block = _output.Pull(1);

        Assert.Contains(block, sample => sample != 0);
        Assert.Equal(new BeatInfo(_session.Status.LastBeat!.Value.Frame, 0), _metronome.LastBeat);

        _metronome.SetRunning(false);
        _output.Pull(1);
        Assert.Null(_metronome.LastBeat);
    }

    [Fact]
    public void CountsNoLateCallbacksWhenPulledInTime()
    {
        _output.Pull(10);

        Assert.Equal(0, _session.Monitor.LateCallbacks);
    }
}
