using System.Diagnostics;
using SoundFlow.Abstracts;
using SoundFlow.Structs;

namespace JustPractise.Prototypes.GcCheck;

/// <summary>
/// Spielt einen Clip in Schleife über signalsmith-stretch ab und misst die Rechenzeit pro Block.
/// Läuft auf dem Audio-Thread: keine Allokationen, keine Locks.
/// </summary>
internal sealed class StretchPlayer(AudioEngine engine, AudioFormat format, AudioClip clip, SignalsmithVoice voice)
    : SoundComponent(engine, format)
{
    private volatile bool _playing;
    private volatile float _tempo = 1f;
    private volatile bool _restart = true;

    public override string Name { get; set; } = "StretchPlayer";

    public ProcessStats Stats { get; } = new(format.SampleRate);

    public bool Playing
    {
        get => _playing;
        set => _playing = value;
    }

    public float Tempo
    {
        get => _tempo;
        set => _tempo = Math.Clamp(value, 0.25f, 2f);
    }

    public double Seconds => (double)voice.Position / clip.SampleRate;

    /// <summary>
    /// Rechnet vor dem Start einige Blöcke ins Leere, damit JIT und erste Berechnungen
    /// nicht im echten Audio-Callback anfallen.
    /// </summary>
    public void WarmUp(int blocks, int blockFrames, int channels)
    {
        var buffer = new float[blockFrames * channels];
        var playing = _playing;
        _playing = true;
        for (var i = 0; i < blocks; i++)
        {
            GenerateAudio(buffer, channels);
        }

        _playing = playing;
        _restart = true;
        Stats.Reset();
    }

    protected override void GenerateAudio(Span<float> buffer, int channels)
    {
        if (!_playing)
        {
            buffer.Clear();
            return;
        }

        var start = Stopwatch.GetTimestamp();
        var tempo = (double)_tempo;
        if (_restart)
        {
            _restart = false;
            voice.Start(clip, 0, tempo);
        }

        voice.Render(clip, buffer, tempo, loop: true);
        Stats.Record(Stopwatch.GetTimestamp() - start, buffer.Length / channels);
    }
}
