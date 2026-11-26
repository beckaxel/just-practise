namespace JustPractise.Audio.Tuner;

/// <summary>
/// Misst laufend die Tonhöhe am Eingang: Solange es läuft, ist das Eingabegerät offen, und alle 40 ms wird das jüngste
/// Fenster gemessen (Prüfpunkt 5: 4096 Samples bei 48 kHz). Die Oberfläche holt sich das letzte Ergebnis ab.
/// </summary>
/// <remarks>Gemessen wird auf einem Thread des Pools, nicht auf dem Audio-Thread und nicht auf dem UI-Thread.</remarks>
public sealed class PitchTracker(IAudioInput input, TimeProvider time) : IDisposable
{
    public static readonly TimeSpan Interval = TimeSpan.FromMilliseconds(40);
    private const int Window = 4096;

    private readonly Lock _lock = new();
    private ITimer? _timer;
    private InputRing? _ring;
    private PitchDetector? _detector;
    private float[]? _buffer;
    private PitchReading _latest;
    private long _measured;

    public bool IsRunning
    {
        get
        {
            lock (_lock)
            {
                return _timer is not null;
            }
        }
    }

    /// <summary>Das jüngste Ergebnis; ohne Messung kein Ton.</summary>
    public PitchReading Latest
    {
        get
        {
            lock (_lock)
            {
                return _latest;
            }
        }
    }

    /// <summary>Wie viele Messungen es bisher gab; ändert sich die Zahl, gibt es ein neues Ergebnis.</summary>
    public long Measured => Interlocked.Read(ref _measured);

    public IReadOnlyList<string> DeviceNames() => input.DeviceNames();

    /// <summary>Öffnet das Eingabegerät und beginnt zu messen; false, wenn es sich nicht öffnen ließ.</summary>
    public bool Start(string? deviceName)
    {
        lock (_lock)
        {
            StopLocked();
            _ring = new InputRing(input.SampleRate * 2);
            _detector = new PitchDetector(input.SampleRate, Window);
            _buffer = new float[Window];
            _latest = default;
            if (!input.Open(deviceName, _ring))
            {
                _ring = null;
                return false;
            }

            _timer = time.CreateTimer(_ => Measure(), null, Interval, Interval);
            return true;
        }
    }

    public void Stop()
    {
        lock (_lock)
        {
            StopLocked();
        }
    }

    public void Dispose() => Stop();

    /// <summary>Misst das jüngste Fenster; der Takt ruft das alle 40 ms auf.</summary>
    public void Measure()
    {
        lock (_lock)
        {
            if (_ring is null || _detector is null || _buffer is null || !_ring.CopyLatest(_buffer))
            {
                return;
            }

            _latest = _detector.Detect(_buffer);
            _measured++;
        }
    }

    private void StopLocked()
    {
        _timer?.Dispose();
        _timer = null;
        if (_ring is not null)
        {
            input.Close();
        }

        _ring = null;
        _latest = default;
    }
}
