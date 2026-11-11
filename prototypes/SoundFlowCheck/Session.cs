using System.Diagnostics;
using SoundFlow.Abstracts.Devices;
using SoundFlow.Backends.MiniAudio;
using SoundFlow.Backends.MiniAudio.Devices;
using SoundFlow.Backends.MiniAudio.Enums;
using SoundFlow.Components;
using SoundFlow.Enums;
using SoundFlow.Providers;
using SoundFlow.Structs;

namespace JustPractise.Prototypes.SoundFlowCheck;

/// <summary>Hält Engine, Geräte und Player einer interaktiven Prüfsitzung.</summary>
internal sealed class Session : IDisposable
{
    private const int SampleRate = 48000;

    private static readonly AudioFormat s_outputFormat = new()
    {
        Format = SampleFormat.F32,
        SampleRate = SampleRate,
        Channels = 2,
        Layout = AudioFormat.GetLayoutFromChannels(2),
    };

    private static readonly AudioFormat s_inputFormat = new()
    {
        Format = SampleFormat.F32,
        SampleRate = SampleRate,
        Channels = 1,
        Layout = AudioFormat.GetLayoutFromChannels(1),
    };

    private readonly MiniAudioEngine _engine;
    private readonly ClickGenerator _click;
    private readonly CallbackStats _stats = new(s_outputFormat.Channels, SampleRate);
    private readonly InputMeter _meter = new(s_inputFormat.Channels, SampleRate);

    private MiniAudioDeviceConfig _config;
    private AudioPlaybackDevice _output;
    private AudioCaptureDevice? _input;
    private SoundPlayer? _player;
    private Stream? _fileStream;
    private StreamDataProvider? _provider;

    public Session(DeviceInfo? output, uint bufferMs)
    {
        _engine = new MiniAudioEngine();
        _config = CreateConfig(bufferMs);
        _output = _engine.InitializePlaybackDevice(output, s_outputFormat, _config);
        _output.OnAudioProcessed += _stats.OnAudioProcessed;
        _click = new ClickGenerator(_engine, s_outputFormat);
        _output.MasterMixer.AddComponent(_click);
        _output.Start();
    }

    public MiniAudioEngine Engine => _engine;

    public ClickGenerator Click => _click;

    public SoundPlayer? Player => _player;

    public uint BufferMs => _config.PeriodSizeInMilliseconds;

    public string OutputName => _output.Info?.Name ?? "Standard";

    public string InputName => _input?.Info?.Name ?? "aus";

    public bool HasInput => _input is not null;

    public CallbackStats Stats => _stats;

    public InputMeter Meter => _meter;

    public static MiniAudioDeviceConfig CreateConfig(uint bufferMs) => new()
    {
        PeriodSizeInMilliseconds = bufferMs,
        Periods = 2,
        Playback = new DeviceSubConfig { ShareMode = ShareMode.Shared },
        Capture = new DeviceSubConfig { ShareMode = ShareMode.Shared },
    };

    public string Load(string path)
    {
        UnloadFile();
        var watch = Stopwatch.StartNew();
        _fileStream = File.OpenRead(path);
        _provider = new StreamDataProvider(_engine, _fileStream);
        _player = new SoundPlayer(_engine, s_outputFormat, _provider);
        _output.MasterMixer.AddComponent(_player);
        watch.Stop();
        return $"{Path.GetFileName(path)}: {_provider.SampleRate} Hz, {_provider.SampleFormat}, " +
            $"{_player.Duration:0.0} s, geladen in {watch.ElapsedMilliseconds} ms";
    }

    public void SwitchOutput(DeviceInfo info, uint bufferMs)
    {
        _config = CreateConfig(bufferMs);
        _output.OnAudioProcessed -= _stats.OnAudioProcessed;
        _output = _engine.SwitchDevice(_output, info, _config);
        _output.OnAudioProcessed += _stats.OnAudioProcessed;
        _stats.Reset();
    }

    public void OpenInput(DeviceInfo? info)
    {
        CloseInput();
        _input = _engine.InitializeCaptureDevice(info, s_inputFormat, _config);
        _input.OnAudioProcessed += _meter.OnAudioProcessed;
        _input.Start();
    }

    public void CloseInput()
    {
        if (_input is null)
        {
            return;
        }

        _input.OnAudioProcessed -= _meter.OnAudioProcessed;
        _input.Stop();
        _input.Dispose();
        _input = null;
    }

    /// <summary>
    /// Spielt einen Klick und wartet, bis er am Eingang ankommt. Ergebnis ist die Zeit vom
    /// Berechnen des Klicks im Callback bis zum Eintreffen am Mikrofon (Ausgabe + Luft + Eingabe).
    /// </summary>
    public double? MeasureRoundTrip()
    {
        _meter.Arm();
        _click.RequestProbe();
        var deadline = Stopwatch.GetTimestamp() + Stopwatch.Frequency;
        while (Stopwatch.GetTimestamp() < deadline)
        {
            Thread.Sleep(5);
            var detected = _meter.DetectedTimestamp;
            var sent = _click.ProbeTimestamp;
            if (detected != 0 && sent != 0)
            {
                return (detected - sent) * 1000.0 / Stopwatch.Frequency;
            }
        }

        return null;
    }

    public void Dispose()
    {
        CloseInput();
        UnloadFile();
        _output.Stop();
        _output.Dispose();
        _engine.Dispose();
    }

    private void UnloadFile()
    {
        if (_player is not null)
        {
            _player.Stop();
            _output.MasterMixer.RemoveComponent(_player);
            _player.Dispose();
            _player = null;
        }

        _provider?.Dispose();
        _provider = null;
        _fileStream?.Dispose();
        _fileStream = null;
    }
}
