using System.Diagnostics;
using SoundFlow.Abstracts;
using SoundFlow.Structs;

namespace JustPractise.Prototypes.StretchCheck;

/// <summary>
/// Spielt einen Clip in Schleife mit wählbarem Verfahren und Tempo ab.
/// Alle Verfahren werden vorab angelegt, ein Wechsel im Callback legt keinen Speicher an.
/// </summary>
internal sealed class StretchPlayer(AudioEngine engine, AudioFormat format, AudioClip clip, IStretchVoice[] voices)
    : SoundComponent(engine, format)
{
    private volatile int _voiceIndex;
    private volatile bool _playing;
    private volatile float _tempo = 1f;
    private long _seekRequest = -1;
    private long _position;
    private int _activeIndex = -1;

    public override string Name { get; set; } = "StretchPlayer";

    public ProcessStats Stats { get; } = new(format.SampleRate);

    public IReadOnlyList<IStretchVoice> Voices => voices;

    public IStretchVoice Voice => voices[_voiceIndex];

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

    public double Seconds => (double)Volatile.Read(ref _position) / clip.SampleRate;

    public void SelectVoice(int index) => _voiceIndex = Math.Clamp(index, 0, voices.Length - 1);

    public void SeekTo(double seconds)
    {
        var frame = (long)(Math.Clamp(seconds, 0, clip.Seconds - 0.1) * clip.SampleRate);
        Interlocked.Exchange(ref _seekRequest, frame);
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
        var index = _voiceIndex;
        var seek = Interlocked.Exchange(ref _seekRequest, -1);
        if (index != _activeIndex || seek >= 0)
        {
            var frame = seek >= 0 ? seek : _activeIndex >= 0 ? voices[_activeIndex].Position : 0;
            voices[index].Start(clip, frame, tempo);
            _activeIndex = index;
        }

        var voice = voices[index];
        voice.Render(clip, buffer, tempo, loop: true);
        Volatile.Write(ref _position, voice.Position);
        Stats.Record(Stopwatch.GetTimestamp() - start, buffer.Length / channels);
    }
}
