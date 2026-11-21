using System.Diagnostics;

namespace JustPractise.Audio.Engine;

/// <summary>
/// Ersatz, wenn kein Ausgabegerät da ist: ruft den Callback im Takt eines Geräts auf, verwirft aber den Ton.
/// So verhalten sich Player und Metronom wie gewohnt (Position, Schlaganzeige), nur ohne Ton.
/// </summary>
public sealed class SilentOutput : IAudioOutput
{
    private const int PeriodMilliseconds = 10;

    private readonly Lock _lock = new();
    private Thread? _thread;
    private volatile bool _running;

    public string? DeviceName => null;

    public bool HasDevice => false;

    public TimeSpan Period => TimeSpan.FromMilliseconds(PeriodMilliseconds);

    public int MixSampleRate => 48000;

    public void Open(int sampleRate, int channels, AudioCallback callback)
    {
        lock (_lock)
        {
            Close();
            var buffer = new float[sampleRate * PeriodMilliseconds / 1000 * channels];
            _running = true;
            _thread = new Thread(() => Run(buffer, callback)) { IsBackground = true, Name = "just practise silent output" };
            _thread.Start();
        }
    }

    public void Close()
    {
        lock (_lock)
        {
            _running = false;
            _thread?.Join();
            _thread = null;
        }
    }

    public void Dispose() => Close();

    private void Run(float[] buffer, AudioCallback callback)
    {
        var clock = Stopwatch.StartNew();
        for (long blocks = 1; _running; blocks++)
        {
            callback(buffer);
            var wait = TimeSpan.FromMilliseconds(blocks * PeriodMilliseconds) - clock.Elapsed;
            if (wait > TimeSpan.Zero)
            {
                Thread.Sleep(wait);
            }
        }
    }
}
