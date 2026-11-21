using System.Diagnostics;
using SoundFlow.Abstracts;
using SoundFlow.Structs;

namespace JustPractise.Prototypes.SoundFlowCheck;

/// <summary>
/// Erzeugt Metronom-Klicks sample-genau direkt im Audio-Callback.
/// Prüft, ob wir eigene Klangerzeugung ohne Umweg in den Graphen von SoundFlow hängen können.
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
    private volatile bool _probeRequested;

    private long _frame;
    private double _nextClickFrame;
    private int _beat;
    private int _clickPosition = int.MaxValue;
    private double _clickFrequency;
    private float _clickGain;

    public ClickGenerator(AudioEngine engine, AudioFormat format) : base(engine, format)
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

    /// <summary>Zeitpunkt (Stopwatch-Ticks), zu dem der letzte Messklick berechnet wurde.</summary>
    public long ProbeTimestamp { get; private set; }

    /// <summary>Spielt im nächsten Callback einen einzelnen, lauten Klick für die Latenzmessung.</summary>
    public void RequestProbe()
    {
        ProbeTimestamp = 0;
        _probeRequested = true;
    }

    protected override void GenerateAudio(Span<float> buffer, int channels)
    {
        var frames = buffer.Length / channels;

        if (_probeRequested)
        {
            _probeRequested = false;
            ProbeTimestamp = Stopwatch.GetTimestamp();
            StartClick(2000, 1.0f);
        }

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
                StartClick(accent ? 1600 : 1000, accent ? 0.8f : 0.5f);
                _beat++;
                _nextClickFrame += framesPerBeat;
            }

            var sample = 0f;
            if (_clickPosition < _clickFrames)
            {
                var t = (double)_clickPosition / _sampleRate;
                var envelope = Math.Exp(-t * 200);
                sample = (float)(Math.Sin(2 * Math.PI * _clickFrequency * t) * envelope) * _clickGain;
                _clickPosition++;
            }

            for (var c = 0; c < channels; c++)
            {
                buffer[(i * channels) + c] = sample;
            }
        }

        _frame += frames;
    }

    private void StartClick(double frequency, float gain)
    {
        _clickFrequency = frequency;
        _clickGain = gain;
        _clickPosition = 0;
    }
}
