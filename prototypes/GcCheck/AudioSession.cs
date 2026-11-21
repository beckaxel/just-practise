using SoundFlow.Abstracts.Devices;
using SoundFlow.Backends.MiniAudio;
using SoundFlow.Backends.MiniAudio.Devices;
using SoundFlow.Backends.MiniAudio.Enums;
using SoundFlow.Enums;
using SoundFlow.Structs;

namespace JustPractise.Prototypes.GcCheck;

/// <summary>Wiedergabe mit Time-Stretching und Metronom auf einem Ausgabegerät, wie später in der App.</summary>
internal sealed class AudioSession : IDisposable
{
    private const int WarmUpBlocks = 50;

    private readonly MiniAudioEngine _engine;
    private readonly AudioPlaybackDevice _device;
    private readonly SignalsmithVoice _voice;

    public AudioSession(Options options)
    {
        _engine = new MiniAudioEngine();
        Clip = AudioClip.Load(_engine, options.File, options.StartSeconds, options.Seconds);

        var format = new AudioFormat
        {
            Format = SampleFormat.F32,
            SampleRate = Clip.SampleRate,
            Channels = Clip.Channels,
            Layout = AudioFormat.GetLayoutFromChannels(Clip.Channels),
        };
        var config = new MiniAudioDeviceConfig
        {
            PeriodSizeInMilliseconds = options.BufferMilliseconds,
            Periods = 2,
            Playback = new DeviceSubConfig { ShareMode = ShareMode.Shared },
        };

        _voice = new SignalsmithVoice("signalsmith", Clip.Channels, Clip.SampleRate, options.StretchOptions);
        _device = _engine.InitializePlaybackDevice(null, format, config);
        Player = new StretchPlayer(_engine, format, Clip, _voice) { Tempo = 0.5f };
        Click = new ClickGenerator(_engine, format);
        Callbacks = new CallbackStats(Clip.Channels, Clip.SampleRate);

        if (options.WarmUp)
        {
            var blockFrames = (int)(Clip.SampleRate * options.BufferMilliseconds / 1000);
            Player.WarmUp(WarmUpBlocks, blockFrames, Clip.Channels);
            Click.WarmUp(WarmUpBlocks, blockFrames, Clip.Channels);
        }

        _device.OnAudioProcessed += Callbacks.OnAudioProcessed;
        _device.MasterMixer.AddComponent(Player);
        _device.MasterMixer.AddComponent(Click);
        _device.Start();
    }

    public AudioClip Clip { get; }

    public StretchPlayer Player { get; }

    public ClickGenerator Click { get; }

    public CallbackStats Callbacks { get; }

    public string DeviceName => _device.Info?.Name ?? "Standard";

    public void Dispose()
    {
        _device.Stop();
        _device.Dispose();
        _voice.Dispose();
        _engine.Dispose();
    }
}
