using SoundFlow.Abstracts;
using SoundFlow.Structs;

namespace JustPractise.Prototypes.GcCheck;

/// <summary>
/// Erzeugt Metronom-Klicks sample-genau im Audio-Callback (aus SoundFlowCheck).
/// Läuft auf dem Audio-Thread: keine Allokationen, keine Locks.
/// </summary>
internal sealed class ClickGenerator : SoundComponent
{
    private const double ClickSeconds = 0.025;
    private const int BeatsPerBar = 4;

    private readonly int _sampleRate;
    private readonly int _clickFrames;

    private volatile bool _running;
    private volatile bool _restart;
    private volatile int _bpm = 100;

    private long _frame;
    private double _nextClickFrame;
    private int _beat;
    private int _clickPosition = int.MaxValue;
    private double _clickFrequency;
    private float _clickGain;

    public ClickGenerator(AudioEngine engine, AudioFormat format)
        : base(engine, format)
    {
        _sampleRate = format.SampleRate;
        _clickFrames = (int)(ClickSeconds * _sampleRate);
    }

    public override string Name { get; set; } = "Click";

    public bool Running
    {
        get => _running;
        set
        {
            _restart = value && !_running;
            _running = value;
        }
    }

    public int Bpm
    {
        get => _bpm;
        set => _bpm = Math.Clamp(value, 20, 400);
    }

    /// <summary>Rechnet vor dem Start einige Blöcke ins Leere (siehe <see cref="StretchPlayer.WarmUp"/>).</summary>
    public void WarmUp(int blocks, int blockFrames, int channels)
    {
        var buffer = new float[blockFrames * channels];
        _running = true;
        for (var i = 0; i < blocks; i++)
        {
            GenerateAudio(buffer, channels);
        }

        _running = false;
    }

    protected override void GenerateAudio(Span<float> buffer, int channels)
    {
        var frames = buffer.Length / channels;
        if (_restart)
        {
            _restart = false;
            _beat = 0;
            _nextClickFrame = _frame;
        }

        var running = _running;
        var framesPerBeat = 60.0 * _sampleRate / _bpm;
        for (var i = 0; i < frames; i++)
        {
            if (running && _frame + i >= (long)_nextClickFrame)
            {
                var accent = _beat % BeatsPerBar == 0;
                _clickFrequency = accent ? 1600 : 1000;
                _clickGain = accent ? 0.6f : 0.4f;
                _clickPosition = 0;
                _beat++;
                _nextClickFrame += framesPerBeat;
            }

            var sample = 0f;
            if (_clickPosition < _clickFrames)
            {
                var t = (double)_clickPosition / _sampleRate;
                sample = (float)(Math.Sin(2 * Math.PI * _clickFrequency * t) * Math.Exp(-t * 200)) * _clickGain;
                _clickPosition++;
            }

            for (var c = 0; c < channels; c++)
            {
                buffer[(i * channels) + c] = sample;
            }
        }

        _frame += frames;
    }
}
