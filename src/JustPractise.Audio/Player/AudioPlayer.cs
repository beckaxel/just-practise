using JustPractise.Audio.Graph;
using JustPractise.Core.Waveform;

namespace JustPractise.Audio.Player;

/// <summary>
/// Der Player für die Oberfläche: lädt Dateien im Hintergrund und schickt Befehle an den Audio-Thread.
/// </summary>
public sealed class AudioPlayer(AudioSession session) : IPlayer
{
    private const int TrackIdMask = 0xFFFF;

    private CancellationTokenSource? _loading;
    private LoadedTrack? _current;
    private string? _path;
    private int _nextId;

    public PlayerState State
    {
        get
        {
            if (_current is not { } track)
            {
                return default;
            }

            var info = session.Status.Player;
            var mine = info.TrackId == (track.Id & TrackIdMask);
            var rate = track.Audio.SampleRate;
            return new PlayerState(
                _path,
                mine && info.IsPlaying,
                TimeSpan.FromSeconds((double)(mine ? info.Position : 0) / rate),
                track.Audio.Duration,
                track.Audio.IsComplete,
                track.Waveform.IsCompletedSuccessfully ? track.Waveform.Result : null);
        }
    }

    public async Task LoadAsync(string path, CancellationToken cancellationToken = default)
    {
        if (_loading is not null)
        {
            await _loading.CancelAsync();
        }

        using var loading = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _loading = loading;
        try
        {
            var track = await OpenAsync(path, loading);
            _current = track;
            _path = path;
            session.LoadTrack(track);
        }
        finally
        {
            if (_loading == loading)
            {
                _loading = null;
            }
        }
    }

    /// <summary>Startet das Decodieren und liefert die Datei, sobald sie abspielbar ist.</summary>
    private async Task<LoadedTrack> OpenAsync(string path, CancellationTokenSource loading)
    {
        var token = loading.Token;
        var opened = new TaskCompletionSource<DecodedAudio>(TaskCreationOptions.RunContinuationsAsynchronously);
        DecodedAudio? audio = null;
        var decoding = Task.Run<WaveformPeaks?>(
            () =>
            {
                try
                {
                    session.Decoder.Decode(
                        path,
                        session.Pool,
                        decoded =>
                        {
                            audio = decoded;
                            opened.TrySetResult(decoded);
                        },
                        token);

                    // Im selben Task, damit die Blöcke erst danach freigegeben werden (siehe AudioSession.Retire).
                    return audio is null ? null : WaveformReader.Read(audio, token);
                }
                finally
                {
                    // Auch bei Abbruch oder Fehler mitten in der Datei: Was da ist, gilt als fertig.
                    audio?.Complete();
                }
            },
            CancellationToken.None);
        _ = decoding.ContinueWith(task => task.Exception, CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);

        if (await Task.WhenAny(opened.Task, decoding) == decoding && !opened.Task.IsCompleted)
        {
            await decoding; // wirft den Grund, z. B. FileNotFoundException oder InvalidDataException
        }

        var decoded = await opened.Task;
        try
        {
            var track = await Task.Run(
                () =>
                {
                    token.ThrowIfCancellationRequested();
                    var id = Interlocked.Increment(ref _nextId);
                    var stretch = new StretchVoice(decoded.Channels, decoded.SampleRate, session.SampleRate);
                    return new LoadedTrack(id, decoded, stretch) { Decoding = decoding, Waveform = decoding };
                },
                token);
            token.ThrowIfCancellationRequested();
            return track;
        }
        catch
        {
            await loading.CancelAsync();
            _ = decoding.ContinueWith(_ => decoded.Release(), CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.Default);
            throw;
        }
    }

    public void Unload()
    {
        _loading?.Cancel();
        _current = null;
        _path = null;
        session.LoadTrack(null);
    }

    public void Play() => SendIfLoaded(AudioCommand.PlayerPlay());

    public void Pause() => SendIfLoaded(AudioCommand.PlayerPause());

    public void Seek(TimeSpan position)
    {
        if (_current is { } track)
        {
            session.Send(AudioCommand.PlayerSeek((long)Math.Max(0, position.TotalSeconds * track.Audio.SampleRate)));
        }
    }

    public void ToStart() => Seek(TimeSpan.Zero);

    public void SetTempo(double tempo) => session.SetPlayerTempo(tempo);

    public void SetVolume(double volumeDb) => session.SetPlayerVolume(volumeDb);

    private void SendIfLoaded(AudioCommand command)
    {
        if (_current is not null)
        {
            session.Send(command);
        }
    }
}
