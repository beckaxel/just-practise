using SoundFlow.Abstracts;
using SoundFlow.Abstracts.Devices;
using SoundFlow.Backends.MiniAudio;
using SoundFlow.Backends.MiniAudio.Devices;
using SoundFlow.Backends.MiniAudio.Enums;
using SoundFlow.Enums;
using SoundFlow.Structs;

namespace JustPractise.Audio.Engine;

/// <summary>
/// Ausgabe über SoundFlow (miniaudio): Standardgerät, geteilter Modus, 10 ms Puffer (Prüfpunkt 1 und 2).
/// Der einzige Ort, an dem SoundFlow-Geräte angefasst werden.
/// </summary>
public sealed class SoundFlowOutput : IAudioOutput
{
    private const uint BufferMilliseconds = 10;

    private readonly Lock _lock = new();
    private AudioPlaybackDevice? _device;
    private CallbackComponent? _component;

    /// <summary>Die Engine; der Decoder nutzt dieselbe.</summary>
    public MiniAudioEngine Engine { get; } = new();

    public string? DeviceName => _device?.Info?.Name;

    public void Open(int sampleRate, int channels, AudioCallback callback)
    {
        lock (_lock)
        {
            Close();
            var format = new AudioFormat
            {
                Format = SampleFormat.F32,
                SampleRate = sampleRate,
                Channels = channels,
                Layout = AudioFormat.GetLayoutFromChannels(channels),
            };
            var config = new MiniAudioDeviceConfig
            {
                PeriodSizeInMilliseconds = BufferMilliseconds,
                Periods = 2,
                Playback = new DeviceSubConfig { ShareMode = ShareMode.Shared },
            };

            _device = Engine.InitializePlaybackDevice(null, format, config);
            _component = new CallbackComponent(Engine, format, callback);
            _device.MasterMixer.AddComponent(_component);
            _device.Start();
        }
    }

    public void Close()
    {
        lock (_lock)
        {
            if (_device is null)
            {
                return;
            }

            _device.Stop();
            _device.MasterMixer.RemoveComponent(_component!);
            _device.Dispose();
            _device = null;
            _component = null;
        }
    }

    public void Dispose()
    {
        Close();
        Engine.Dispose();
    }

    /// <summary>Reicht den Callback von SoundFlow an <see cref="AudioCallback"/> weiter.</summary>
    private sealed class CallbackComponent(AudioEngine engine, AudioFormat format, AudioCallback callback)
        : SoundComponent(engine, format)
    {
        public override string Name { get; set; } = "just practise";

        protected override void GenerateAudio(Span<float> buffer, int channels) => callback(buffer);
    }
}
