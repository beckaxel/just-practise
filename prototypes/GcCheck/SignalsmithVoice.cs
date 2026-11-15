namespace JustPractise.Prototypes.GcCheck;

/// <summary>Time-Stretching mit signalsmith-stretch über native/stretch.</summary>
internal sealed class SignalsmithVoice : IDisposable
{
    private const int MaxChunkFrames = 4096;
    private const double MaxTempo = 2.0;

    private readonly StretchHandle _handle;
    private readonly int _channels;
    private readonly int _seekLength;
    private readonly float[] _input;

    private long _position;
    private double _fraction;

    public SignalsmithVoice(string name, int channels, int sampleRate, int options)
    {
        if (NativeMethods.GetApiVersion() != NativeMethods.ApiVersion)
        {
            throw new InvalidOperationException("jpstretch hat eine andere Version der Schnittstelle.");
        }

        _handle = NativeMethods.Create(channels, sampleRate, options);
        if (_handle.IsInvalid)
        {
            throw new InvalidOperationException("jp_stretch_create ist fehlgeschlagen.");
        }

        Name = name;
        _channels = channels;
        _seekLength = NativeMethods.SeekLength(_handle);
        _input = new float[Math.Max(_seekLength, (int)(MaxChunkFrames * MaxTempo) + 1) * channels];
        LatencyFrames = NativeMethods.InputLatency(_handle) + NativeMethods.OutputLatency(_handle);
    }

    public string Name { get; }

    public long Position => _position;

    public int LatencyFrames { get; }

    public void Start(AudioClip clip, long frame, double tempo)
    {
        var preRoll = _input.AsSpan(0, _seekLength * _channels);
        clip.Read(frame - _seekLength, preRoll, loop: false);
        NativeMethods.Reset(_handle);
        NativeMethods.Seek(_handle, preRoll, _seekLength, tempo);
        _position = frame;
        _fraction = 0;
    }

    public void Render(AudioClip clip, Span<float> output, double tempo, bool loop)
    {
        tempo = Math.Clamp(tempo, 0.01, MaxTempo);
        var totalFrames = output.Length / _channels;
        var done = 0;
        while (done < totalFrames)
        {
            var frames = Math.Min(MaxChunkFrames, totalFrames - done);
            _fraction += frames * tempo;
            var inputFrames = (int)_fraction;
            _fraction -= inputFrames;

            var input = _input.AsSpan(0, inputFrames * _channels);
            clip.Read(_position, input, loop);
            NativeMethods.Process(_handle, input, inputFrames, output.Slice(done * _channels, frames * _channels), frames);

            _position += inputFrames;
            if (loop)
            {
                _position %= clip.Frames;
            }

            done += frames;
        }
    }

    public void Dispose() => _handle.Dispose();
}
