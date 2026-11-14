using SoundFlow.Components;

namespace JustPractise.Prototypes.StretchCheck;

/// <summary>
/// Time-Stretching mit dem WSOLA-Verfahren aus SoundFlow.
/// <see cref="WsolaTimeStretcher.Process"/> liefert pro Aufruf einen ganzen Block und verwirft,
/// was nicht in die Ausgabe passt. Deshalb geht die Ausgabe über einen eigenen Zwischenpuffer.
/// </summary>
internal sealed class WsolaVoice : IStretchVoice
{
    private const int FeedFrames = 1024;

    private readonly WsolaTimeStretcher _wsola;
    private readonly int _channels;
    private readonly int _windowFrames;
    private readonly float[] _feed;
    private readonly float[] _pending;

    private int _pendingStart;
    private int _pendingEnd;
    private long _position;
    private float _speed = 1f;

    public WsolaVoice(string name, int channels, WsolaPerformancePreset preset)
    {
        var config = WsolaConfig.FromPreset(preset);
        Name = name;
        _channels = channels;
        _windowFrames = config.WindowSizeFrames;
        _wsola = new WsolaTimeStretcher(channels, 1f, config);
        _feed = new float[FeedFrames * channels];
        _pending = new float[config.WindowSizeFrames * 2 * channels];
    }

    public string Name { get; }

    public long Position => _position;

    public int LatencyFrames => _windowFrames;

    public void Start(AudioClip clip, long frame, double tempo)
    {
        _wsola.Reset();
        SetSpeed(tempo);
        _pendingStart = 0;
        _pendingEnd = 0;
        _position = frame;
    }

    public void Render(AudioClip clip, Span<float> output, double tempo, bool loop)
    {
        SetSpeed(tempo);
        var written = 0;
        var idle = 0;
        while (written < output.Length)
        {
            if (_pendingStart < _pendingEnd)
            {
                var count = Math.Min(_pendingEnd - _pendingStart, output.Length - written);
                _pending.AsSpan(_pendingStart, count).CopyTo(output[written..]);
                _pendingStart += count;
                written += count;
                continue;
            }

            clip.Read(_position, _feed, loop);
            var produced = _wsola.Process(_feed, _pending, out var consumed, out _);
            _pendingStart = 0;
            _pendingEnd = produced;
            _position += consumed / _channels;
            if (loop)
            {
                _position %= clip.Frames;
            }

            idle = produced == 0 && consumed == 0 ? idle + 1 : 0;
            if (idle > 4)
            {
                output[written..].Clear();
                return;
            }
        }
    }

    public void Dispose()
    {
    }

    private void SetSpeed(double tempo)
    {
        var speed = (float)tempo;
        if (speed != _speed)
        {
            _wsola.SetSpeed(speed);
            _speed = speed;
        }
    }
}
