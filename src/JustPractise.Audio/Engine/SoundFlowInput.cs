using JustPractise.Audio.Tuner;
using SoundFlow.Abstracts.Devices;
using SoundFlow.Backends.MiniAudio;
using SoundFlow.Backends.MiniAudio.Devices;
using SoundFlow.Backends.MiniAudio.Enums;
using SoundFlow.Enums;
using SoundFlow.Structs;

namespace JustPractise.Audio.Engine;

/// <summary>
/// Eingang über SoundFlow (miniaudio): mono, 48 kHz, geteilter Modus, 10 ms Puffer (Prüfpunkt 5). Nutzt dieselbe Engine
/// wie die Ausgabe.
/// </summary>
public sealed class SoundFlowInput(MiniAudioEngine engine) : IAudioInput, IDisposable
{
    private readonly Lock _lock = new();
    private AudioCaptureDevice? _device;
    private InputRing? _ring;

    public int SampleRate => 48000;

    public IReadOnlyList<string> DeviceNames()
    {
        try
        {
            engine.UpdateAudioDevicesInfo();
            return [.. engine.CaptureDevices.Select(device => device.Name)];
        }
        catch (Exception exception) when (exception is InvalidOperationException or SoundFlow.Exceptions.BackendException)
        {
            return [];
        }
    }

    public bool Open(string? deviceName, InputRing ring)
    {
        lock (_lock)
        {
            StopLocked();
            try
            {
                engine.UpdateAudioDevicesInfo();
                DeviceInfo? device = deviceName is null ? null : engine.CaptureDevices.FirstOrDefault(info => info.Name == deviceName);
                var format = new AudioFormat
                {
                    Format = SampleFormat.F32,
                    SampleRate = SampleRate,
                    Channels = 1,
                    Layout = AudioFormat.GetLayoutFromChannels(1),
                };
                var config = new MiniAudioDeviceConfig
                {
                    PeriodSizeInMilliseconds = 10,
                    Periods = 2,
                    Capture = new DeviceSubConfig { ShareMode = ShareMode.Shared },
                };
                _ring = ring;
                _device = engine.InitializeCaptureDevice(device, format, config);
                _device.OnAudioProcessed += OnAudio;
                _device.Start();
                return true;
            }
            catch (Exception exception) when (exception is InvalidOperationException or SoundFlow.Exceptions.BackendException)
            {
                StopLocked();
                return false;
            }
        }
    }

    public void Close()
    {
        lock (_lock)
        {
            StopLocked();
        }
    }

    public void Dispose() => Close();

    /// <summary>Auf dem Audio-Thread: nur in den Ring schreiben.</summary>
    private void OnAudio(Span<float> samples, Capability capability) => _ring?.Write(samples, 1);

    private void StopLocked()
    {
        if (_device is null)
        {
            return;
        }

        _device.OnAudioProcessed -= OnAudio;
        _device.Stop();
        _device.Dispose();
        _device = null;
        _ring = null;
    }
}
