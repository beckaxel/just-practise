using JustPractise.Core.Pieces;

namespace JustPractise.Audio.Player;

/// <summary>
/// Spielt eine Datei über den Stretcher ab: Starten, Anhalten, Springen, Tempo, Lautstärke, Halt am Ende.
/// </summary>
/// <remarks>
/// Läuft auf dem Audio-Thread: keine Allokation, keine Sperre. Starten und Springen nutzen <c>outputSeek</c>,
/// damit die Ausgabe genau an der Zielstelle beginnt. Starten, Anhalten und Springen blenden kurz ein bzw. aus,
/// Lautstärkeänderungen gleiten, damit nichts knackst.
/// Die Position zählt in Frames der Datei und ist die Stelle, die gerade zu hören ist.
/// </remarks>
public sealed class PlayerVoice
{
    private LoadedTrack? _track;
    private bool _playing;
    private bool _startPending;
    private bool _pauseRequested;
    private long _pendingSeek = -1;
    private double _position;
    private long _inputPosition;
    private double _inputFraction;
    private double _tempo = 1.0;
    private float _gain = 1f;
    private float _targetGain = 1f;
    private int _fadeInRemaining;

    public bool IsPlaying => _playing;

    public long Position => (long)Math.Round(_position);

    public int TrackId => _track?.Id ?? 0;

    /// <summary>Setzt eine neue Datei ein, angehalten am Anfang. Die bisherige kommt über <paramref name="previous"/> zurück.</summary>
    public void Load(LoadedTrack? track, out LoadedTrack? previous)
    {
        previous = _track;
        _track = track;
        _playing = false;
        _startPending = false;
        _pauseRequested = false;
        _pendingSeek = -1;
        _position = 0;
    }

    public void Play()
    {
        if (_track is not { } track)
        {
            return;
        }

        if (track.Audio.IsComplete && _position >= track.Audio.Frames)
        {
            _position = 0;
        }

        _playing = true;
        _startPending = true;
        _pauseRequested = false;
    }

    public void Pause()
    {
        if (_playing)
        {
            _pauseRequested = true;
        }
    }

    public void Seek(long frame) => _pendingSeek = Math.Max(0, frame);

    public void SetTempo(double tempo) => _tempo = Math.Clamp(tempo, FileSettings.MinTempo, FileSettings.MaxTempo);

    public void SetVolume(double volumeDb) => _targetGain = Decibels.ToGain(volumeDb);

    /// <summary>Mischt diesen Block in <paramref name="buffer"/> (interleaved, <paramref name="channels"/> Kanäle).</summary>
    public void Render(Span<float> buffer, int channels)
    {
        if (_track is not { } track)
        {
            return;
        }

        var frames = buffer.Length / channels;
        var done = 0;

        if (_pendingSeek >= 0)
        {
            var target = Math.Min(_pendingSeek, track.Audio.IsComplete ? track.Audio.Frames : long.MaxValue);
            _pendingSeek = -1;
            if (_playing && !_startPending)
            {
                done += RenderFadeOut(track, buffer, channels, done, frames - done);
            }

            _position = target;
            _startPending = _playing;
        }

        if (_pauseRequested)
        {
            _pauseRequested = false;
            if (_playing && !_startPending)
            {
                done += RenderFadeOut(track, buffer, channels, done, frames - done);
            }

            _playing = false;
            _startPending = false;
        }

        if (!_playing)
        {
            return;
        }

        if (_startPending)
        {
            OutputSeek(track);
            _startPending = false;
            _fadeInRemaining = track.FadeFrames;
        }

        Render(track, buffer, channels, done, frames - done, fadeOutFrames: 0);

        if (track.Audio.IsComplete && _position >= track.Audio.Frames)
        {
            _playing = false;
            _position = track.Audio.Frames;
        }
    }

    private int RenderFadeOut(LoadedTrack track, Span<float> buffer, int channels, int offset, int available)
    {
        var frames = Math.Min(track.FadeFrames, available);
        Render(track, buffer, channels, offset, frames, fadeOutFrames: frames);
        return frames;
    }

    private void OutputSeek(LoadedTrack track)
    {
        var start = (long)_position;
        var frames = track.Stretch.OutputSeekLength(_tempo);
        var input = track.Input.AsSpan(0, frames * track.Audio.Channels);
        ReadInput(track, start, input);
        track.Stretch.OutputSeek(input, frames);
        _inputPosition = start + frames;
        _inputFraction = 0;
        _position = start;
    }

    /// <summary>Berechnet <paramref name="count"/> Frames und mischt sie ab <paramref name="offset"/> in den Puffer.</summary>
    private void Render(LoadedTrack track, Span<float> buffer, int channels, int offset, int count, int fadeOutFrames)
    {
        var fileChannels = track.Audio.Channels;
        var done = 0;
        while (done < count)
        {
            var frames = Math.Min(LoadedTrack.MaxChunkFrames, count - done);
            _inputFraction += frames * _tempo;
            var inputFrames = (int)_inputFraction;
            _inputFraction -= inputFrames;

            var input = track.Input.AsSpan(0, inputFrames * fileChannels);
            var output = track.Output.AsSpan(0, frames * fileChannels);
            ReadInput(track, _inputPosition, input);
            track.Stretch.Process(input, inputFrames, output, frames);
            _inputPosition += inputFrames;
            _position += frames * _tempo;

            for (var i = 0; i < frames; i++)
            {
                _gain += (_targetGain - _gain) * track.Smoothing;
                var fade = 1f;
                if (fadeOutFrames > 0)
                {
                    fade = 1f - ((float)(done + i + 1) / fadeOutFrames);
                }
                else if (_fadeInRemaining > 0)
                {
                    fade = 1f - ((float)_fadeInRemaining / track.FadeFrames);
                    _fadeInRemaining--;
                }

                var gain = _gain * fade;
                var source = i * fileChannels;
                var target = (offset + done + i) * channels;
                for (var c = 0; c < channels; c++)
                {
                    buffer[target + c] += output[source + Math.Min(c, fileChannels - 1)] * gain;
                }
            }

            done += frames;
        }
    }

    /// <summary>Liest Eingabe ab <paramref name="frame"/>; was es noch nicht oder nicht mehr gibt, ist Stille.</summary>
    private static void ReadInput(LoadedTrack track, long frame, Span<float> destination)
    {
        var read = track.Audio.Read(frame, destination);
        destination[read..].Clear();
    }
}
