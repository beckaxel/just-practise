using System.Diagnostics;
using JustPractise.Audio.Engine;
using JustPractise.Audio.Graph;
using JustPractise.Audio.Metronome;
using JustPractise.Audio.Player;
using JustPractise.Core.Metronome;

namespace JustPractise.Audio;

/// <summary>
/// Hält Ausgabegerät, Audio-Callback, Befehls- und Rückgabe-Ring sowie den Blockpool zusammen.
/// </summary>
/// <remarks>
/// Das Gerät läuft mit der Rate seines Mischers und bleibt offen, auch wenn Dateien mit anderer Rate geladen
/// werden; der Stretcher des Players rechnet um. So rechnet weder miniaudio (nur linear) noch das System um, und
/// die Callbacks kommen gleichmäßig. Vor dem Öffnen rechnet die Sitzung einige Blöcke ins Leere, damit JIT und
/// erste Berechnungen nicht im echten Callback anfallen.
/// Ersetzte Dateien werden regelmäßig freigegeben, aber erst, wenn ihr Decoder fertig ist.
/// </remarks>
public sealed class AudioSession : IAudioDevice, IDisposable
{
    public const int Channels = 2;

    private const int WarmUpBlocks = 50;
    private const int WarmUpBlockFrames = 480;
    private static readonly TimeSpan CleanupInterval = TimeSpan.FromMilliseconds(200);

    private readonly IAudioOutput _output;
    private readonly Lock _lock = new();
    private readonly Lock _cleanupLock = new();
    private readonly CommandRing<AudioCommand> _commands = new(256);
    private readonly CommandRing<RetiredTrack> _retired = new(64);
    private readonly ITimer _cleanup;

    private PlayerVoice? _player;
    private LoadedTrack? _current;
    private int _bpm = 120;
    private Meter _meter = Meter.FourFour;
    private double _metronomeVolumeDb;
    private bool _metronomeRunning;
    private double _playerTempo = 1.0;
    private double _playerVolumeDb;

    public AudioSession(IAudioOutput output, IAudioDecoder decoder, TimeProvider timeProvider)
    {
        _output = output;
        Decoder = decoder;
        Monitor = new CallbackMonitor(output.Period);
        var sampleRate = output.MixSampleRate;
        WarmUp(sampleRate);
        lock (_lock)
        {
            Rebuild(sampleRate);
        }

        _cleanup = timeProvider.CreateTimer(_ => ReleaseRetired(), null, CleanupInterval, CleanupInterval);
    }

    public IAudioDecoder Decoder { get; }

    public BlockPool Pool { get; } = new();

    public AudioStatus Status { get; } = new();

    public CallbackMonitor Monitor { get; }

    public int SampleRate { get; private set; }

    public string? DeviceName => _output.DeviceName;

    bool IAudioDevice.IsAvailable => _output.HasDevice;

    string? IAudioDevice.Name => _output.DeviceName;

    long IAudioDevice.LateCallbacks => Monitor.LateCallbacks;

    internal bool MetronomeRunning => _metronomeRunning;

    internal int Bpm => _bpm;

    internal Meter Meter => _meter;

    /// <summary>Gibt Dateien frei, die der Audio-Thread zurückgegeben hat. Läuft regelmäßig von selbst.</summary>
    public void ReleaseRetired()
    {
        lock (_cleanupLock)
        {
            while (_retired.TryDequeue(out var retired))
            {
                Retire(retired.Track);
            }
        }
    }

    public void Dispose()
    {
        _cleanup.Dispose();
        lock (_lock)
        {
            _output.Close();
            DrainCommands();
            RetirePlayerTrack();
        }

        ReleaseRetired();
    }

    internal void LoadTrack(LoadedTrack? track)
    {
        lock (_lock)
        {
            _current = track;
            Send(AudioCommand.PlayerLoad(track));
        }
    }

    internal void SetPlayerTempo(double tempo)
    {
        lock (_lock)
        {
            _playerTempo = tempo;
            Send(AudioCommand.PlayerTempo(tempo));
        }
    }

    internal void SetPlayerVolume(double volumeDb)
    {
        lock (_lock)
        {
            _playerVolumeDb = volumeDb;
            Send(AudioCommand.PlayerVolume(volumeDb));
        }
    }

    internal void SetMetronomeRunning(bool running)
    {
        lock (_lock)
        {
            _metronomeRunning = running;
            Send(running ? AudioCommand.MetronomeStart() : AudioCommand.MetronomeStop());
        }
    }

    internal void SetMetronomeTempo(int bpm)
    {
        lock (_lock)
        {
            _bpm = Math.Clamp(bpm, BeatClock.MinBpm, BeatClock.MaxBpm);
            Send(AudioCommand.MetronomeTempo(_bpm));
        }
    }

    internal void SetMetronomeMeter(Meter meter)
    {
        lock (_lock)
        {
            _meter = meter;
            Send(AudioCommand.MetronomeMeter(meter));
        }
    }

    internal void SetMetronomeVolume(double volumeDb)
    {
        lock (_lock)
        {
            _metronomeVolumeDb = volumeDb;
            Send(AudioCommand.MetronomeVolume(volumeDb));
        }
    }

    internal void Send(AudioCommand command)
    {
        lock (_lock)
        {
            if (!_commands.TryEnqueue(command))
            {
                throw new InvalidOperationException("The audio command ring is full; the audio thread does not seem to run.");
            }
        }
    }

    /// <summary>Gibt eine Datei frei, sobald ihr Decoder fertig ist, und verkleinert danach den Pool.</summary>
    private void Retire(LoadedTrack track) =>
        track.Decoding.ContinueWith(
            _ =>
            {
                track.Dispose();
                Pool.Trim(Volatile.Read(ref _current)?.Audio.BlockCount ?? 0);
            },
            CancellationToken.None,
            TaskContinuationOptions.None,
            TaskScheduler.Default);

    /// <summary>Öffnet das Gerät mit dieser Rate und baut den Callback auf. Nur unter <see cref="_lock"/>.</summary>
    private void Rebuild(int sampleRate)
    {
        _output.Close();
        Monitor.Restart();
        DrainCommands();
        RetirePlayerTrack();

        var player = new PlayerVoice();
        player.SetTempo(_playerTempo);
        player.SetVolume(_playerVolumeDb);
        var click = new ClickVoice(new BeatClock(sampleRate, _bpm, _meter), ClickSound.Create(sampleRate));
        click.SetVolume(_metronomeVolumeDb);
        var graph = new AudioGraph(Channels, player, click, _commands, _retired, Status);
        if (_metronomeRunning)
        {
            _commands.TryEnqueue(AudioCommand.MetronomeStart());
        }

        _player = player;
        SampleRate = sampleRate;
        _output.Open(sampleRate, Channels, buffer =>
        {
            Monitor.Tick(Stopwatch.GetTimestamp(), buffer.Length / Channels, sampleRate);
            graph.Render(buffer);
        });
    }

    /// <summary>Nimmt die Datei aus dem Player, während kein Callback läuft.</summary>
    private void RetirePlayerTrack()
    {
        LoadedTrack? previous = null;
        _player?.Load(null, out previous);
        if (previous is not null)
        {
            Retire(previous);
        }
    }

    /// <summary>Verwirft wartende Befehle, während kein Callback läuft. Darin steckende Dateien werden freigegeben.</summary>
    private void DrainCommands()
    {
        while (_commands.TryDequeue(out var command))
        {
            if (command.Payload is LoadedTrack track)
            {
                Retire(track);
            }
        }
    }

    /// <summary>Rechnet einige Blöcke mit Player und Metronom ins Leere, ohne Gerät.</summary>
    private static void WarmUp(int sampleRate)
    {
        var commands = new CommandRing<AudioCommand>(16);
        var player = new PlayerVoice();
        var graph = new AudioGraph(
            Channels,
            player,
            new ClickVoice(new BeatClock(sampleRate, 120, Meter.FourFour), ClickSound.Create(sampleRate)),
            commands,
            new CommandRing<RetiredTrack>(4),
            new AudioStatus());

        LoadedTrack? track = null;
        if (StretchVoice.IsAvailable(out _))
        {
            var audio = new DecodedAudio(new BlockPool(blockSamples: sampleRate * Channels), Channels, sampleRate);
            var tail = audio.WritableTail(sampleRate * Channels);
            tail.Clear();
            audio.Commit(tail.Length);
            audio.Complete();
            track = new LoadedTrack(0, audio, new StretchVoice(Channels, sampleRate, sampleRate));
            commands.TryEnqueue(AudioCommand.PlayerLoad(track));
            commands.TryEnqueue(AudioCommand.PlayerTempo(0.8));
            commands.TryEnqueue(AudioCommand.PlayerPlay());
        }

        commands.TryEnqueue(AudioCommand.MetronomeStart());
        var buffer = new float[WarmUpBlockFrames * Channels];
        for (var i = 0; i < WarmUpBlocks; i++)
        {
            if (i == WarmUpBlocks / 2)
            {
                commands.TryEnqueue(AudioCommand.PlayerSeek(sampleRate / 4));
            }

            graph.Render(buffer);
        }

        track?.Dispose();
    }
}
