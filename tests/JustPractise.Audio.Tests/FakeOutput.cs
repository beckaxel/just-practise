using JustPractise.Audio.Engine;

namespace JustPractise.Audio.Tests;

/// <summary>Ein Ausgabegerät ohne Ton: Der Test zieht die Blöcke selbst mit <see cref="Pull"/>.</summary>
internal sealed class FakeOutput : IAudioOutput
{
    private AudioCallback? _callback;
    private int _channels;

    public List<int> OpenedRates { get; } = [];

    public bool IsOpen => _callback is not null;

    public string? DeviceName => IsOpen ? "Fake" : null;

    public TimeSpan Period => TimeSpan.FromMilliseconds(10);

    public int MixSampleRate { get; init; } = 48000;

    public void Open(int sampleRate, int channels, AudioCallback callback)
    {
        OpenedRates.Add(sampleRate);
        _channels = channels;
        _callback = callback;
    }

    public void Close() => _callback = null;

    /// <summary>Ruft den Callback für Blöcke zu <paramref name="frames"/> Frames auf und liefert den letzten Block.</summary>
    public float[] Pull(int blocks, int frames = 480)
    {
        var buffer = new float[frames * _channels];
        for (var i = 0; i < blocks; i++)
        {
            _callback?.Invoke(buffer);
        }

        return buffer;
    }

    public void Dispose() => Close();
}
