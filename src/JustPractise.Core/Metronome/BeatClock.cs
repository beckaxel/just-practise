namespace JustPractise.Core.Metronome;

/// <summary>Ein Schlag des Metronoms.</summary>
/// <param name="Frame">das Sample, in dem der Schlag beginnt</param>
/// <param name="IndexInBar">der Schlag im Takt, 0 ist die Eins</param>
/// <param name="Accented">betont (vorerst immer die Eins)</param>
public readonly record struct Beat(long Frame, int IndexInBar, bool Accented);

/// <summary>
/// Der Taktgeber des Metronoms: Er rechnet in Samples aus, wann welcher Schlag kommt.
/// </summary>
/// <remarks>
/// Gehört dem Audio-Thread: nicht threadsicher, legt keinen Speicher an. Änderungen aus der Oberfläche
/// reicht die Audio-Schicht über eine lock-freie Warteschlange herein.
/// Die Schläge werden von einem Ankerpunkt aus berechnet statt aufaddiert, damit sich über lange Zeit
/// keine Rundungsfehler ansammeln. Ein Schlag ist eine Zählzeit der Taktart; die BPM beziehen sich auf sie.
/// </remarks>
public sealed class BeatClock
{
    public const int MinBpm = 30;
    public const int MaxBpm = 250;

    private readonly int _sampleRate;

    private double _framesPerBeat;
    private double _anchor;
    private long _beatsSinceAnchor;
    private double _lastBeat;
    private bool _hasLastBeat;
    private int _nextIndexInBar;

    public BeatClock(int sampleRate, int bpm, Meter meter)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sampleRate);
        _sampleRate = sampleRate;
        Meter = meter;
        SetBpm(bpm);
    }

    public int Bpm { get; private set; }

    public Meter Meter { get; private set; }

    public bool IsRunning { get; private set; }

    /// <summary>Startet mit der Eins im angegebenen Sample.</summary>
    public void Start(long frame)
    {
        _anchor = frame;
        _beatsSinceAnchor = 0;
        _hasLastBeat = false;
        _nextIndexInBar = 0;
        IsRunning = true;
    }

    public void Stop() => IsRunning = false;

    /// <summary>
    /// Ändert das Tempo. Der nächste Schlag kommt im neuen Abstand zum letzten Schlag,
    /// liegt das schon zurück, sofort im Sample <paramref name="now"/>.
    /// </summary>
    public void SetTempo(int bpm, long now)
    {
        SetBpm(bpm);
        if (!IsRunning || !_hasLastBeat)
        {
            return;
        }

        _anchor = Math.Max(_lastBeat + _framesPerBeat, now);
        _beatsSinceAnchor = 0;
    }

    /// <summary>Ändert die Taktart. Passt der nächste Schlag nicht mehr in den Takt, kommt als Nächstes die Eins.</summary>
    public void SetMeter(Meter meter)
    {
        Meter = meter;
        if (_nextIndexInBar >= meter.Beats)
        {
            _nextIndexInBar = 0;
        }
    }

    /// <summary>
    /// Liefert den nächsten Schlag, falls er vor <paramref name="endFrame"/> beginnt.
    /// Der Audio-Callback ruft das pro Block so lange auf, bis es <c>false</c> liefert.
    /// </summary>
    public bool TryNextBeat(long endFrame, out Beat beat)
    {
        var time = _anchor + (_beatsSinceAnchor * _framesPerBeat);
        var frame = (long)Math.Round(time, MidpointRounding.AwayFromZero);
        if (!IsRunning || frame >= endFrame)
        {
            beat = default;
            return false;
        }

        beat = new Beat(frame, _nextIndexInBar, _nextIndexInBar == 0);
        _lastBeat = time;
        _hasLastBeat = true;
        _beatsSinceAnchor++;
        _nextIndexInBar = (_nextIndexInBar + 1) % Meter.Beats;
        return true;
    }

    private void SetBpm(int bpm)
    {
        Bpm = Math.Clamp(bpm, MinBpm, MaxBpm);
        _framesPerBeat = 60.0 * _sampleRate / Bpm;
    }
}
