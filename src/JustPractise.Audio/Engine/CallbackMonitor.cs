using System.Diagnostics;

namespace JustPractise.Audio.Engine;

/// <summary>
/// Zählt verspätete Audio-Callbacks (Prüfpunkt 1), etwa weil eine Speicherbereinigung den Audio-Thread angehalten hat.
/// </summary>
/// <remarks>
/// <para>
/// Gemessen wird der Vorlauf: wie viel Audio die Callbacks gegenüber der verstrichenen Zeit geliefert haben.
/// Jeder Callback bringt einen Block, die Zeit bis zum nächsten zehrt davon. Ein Callback gilt als verspätet, wenn
/// der Vorlauf bei seiner Ankunft unter −0,8 Perioden gefallen ist. Bei gleichmäßigen Callbacks ist das dieselbe
/// Grenze wie das 1,8-Fache der Periode aus Prüfpunkt 1.
/// </para>
/// <para>
/// Rechnet das Gerät die Rate um (unter Windows z. B. 44,1 kHz auf die 48 kHz des Mischers), kommen die Callbacks
/// unregelmäßig: mal zwei kurz hintereinander, dann eine Periode lang keiner, im Mittel aber im Takt. Der Vorlauf darf
/// deshalb bis auf zwei Perioden steigen und den folgenden langen Abstand auffangen. Er strebt langsam zu einer Periode
/// zurück, damit kleine Gangunterschiede zwischen Geräte- und Systemuhr sich nicht aufsummieren.
/// </para>
/// <para>
/// Beim Anlaufen lässt das Gerät manchmal eine lange Pause, unter PipeWire bis 100 ms nach dem ersten Callback.
/// Das ist kein Aussetzer, denn bis dahin hat noch nichts gespielt. Die erste halbe Sekunde nach dem Öffnen zählt
/// deshalb nicht mit.
/// </para>
/// <para>Läuft auf dem Audio-Thread: keine Allokation, keine Sperre.</para>
/// </remarks>
public sealed class CallbackMonitor(TimeSpan period)
{
    /// <summary>Unter −0,8 Perioden Vorlauf gilt ein Callback als verspätet (das 1,8-Fache aus Prüfpunkt 1).</summary>
    private const double LateLead = -0.8;

    /// <summary>Höchstens zwei Perioden Vorlauf werden angerechnet.</summary>
    private const double MaxLead = 2.0;

    /// <summary>Anteil, um den der Vorlauf je Callback zu einer Periode zurückstrebt (Zeitkonstante rund 100 Callbacks).</summary>
    private const double Leak = 0.01;

    /// <summary>So lange nach dem ersten Callback läuft das Gerät noch an und nichts wird gezählt.</summary>
    public static readonly TimeSpan StartupTime = TimeSpan.FromSeconds(0.5);

    private static readonly long StartupTicks = (long)(StartupTime.TotalSeconds * Stopwatch.Frequency);

    private readonly double _periodTicks = period.TotalSeconds * Stopwatch.Frequency;
    private long _last;
    private long _countFrom;
    private double _lead;
    private long _late;
    private long _maxGapTicks;
    private long _minLeadTicks;
    private int _minFrames = int.MaxValue;
    private int _maxFrames;

    public long LateCallbacks => Volatile.Read(ref _late);

    public TimeSpan MaxGap => FromTicks(Volatile.Read(ref _maxGapTicks));

    /// <summary>Geringster Vorlauf bei Ankunft eines Callbacks seit dem Öffnen des Geräts; negativ heißt hinter dem Plan.</summary>
    public TimeSpan MinLead => FromTicks(Volatile.Read(ref _minLeadTicks));

    /// <summary>Kleinster Block in Frames seit dem Öffnen des Geräts, 0 vor dem ersten Callback.</summary>
    public int MinFrames => Volatile.Read(ref _maxFrames) == 0 ? 0 : Volatile.Read(ref _minFrames);

    /// <summary>Größter Block in Frames seit dem Öffnen des Geräts.</summary>
    public int MaxFrames => Volatile.Read(ref _maxFrames);

    /// <summary>Meldet einen Callback über <paramref name="frames"/> Frames zum Zeitpunkt <paramref name="timestamp"/> (Stopwatch-Ticks).</summary>
    public void Tick(long timestamp, int frames, int sampleRate)
    {
        if (frames < _minFrames)
        {
            Volatile.Write(ref _minFrames, frames);
        }

        if (frames > _maxFrames)
        {
            Volatile.Write(ref _maxFrames, frames);
        }

        var block = (double)frames / sampleRate * Stopwatch.Frequency;
        var unit = Math.Max(_periodTicks, block);
        if (_last == 0)
        {
            _lead = unit;
            _countFrom = timestamp + StartupTicks;
        }
        else if (timestamp < _countFrom)
        {
            _lead = unit;
        }
        else
        {
            var gap = timestamp - _last;
            if (gap > _maxGapTicks)
            {
                Volatile.Write(ref _maxGapTicks, gap);
            }

            var arrival = _lead - gap;
            if (arrival < _minLeadTicks)
            {
                Volatile.Write(ref _minLeadTicks, (long)arrival);
            }

            if (arrival < LateLead * unit)
            {
                Interlocked.Increment(ref _late);
                _lead = unit; // Eine Lücke zählt einmal; danach beginnt der Plan neu.
            }
            else
            {
                _lead = Math.Min(arrival + block, MaxLead * unit);
                _lead += (unit - _lead) * Leak;
            }
        }

        _last = timestamp;
    }

    /// <summary>
    /// Beginnt neu, z. B. nachdem das Gerät neu geöffnet wurde; die Lücke dazwischen zählt nicht.
    /// Blockgrößen und geringster Vorlauf gelten danach für das neue Gerät. Nur aufrufen, während kein Callback läuft.
    /// </summary>
    public void Restart()
    {
        _last = 0;
        Volatile.Write(ref _minLeadTicks, 0);
        Volatile.Write(ref _minFrames, int.MaxValue);
        Volatile.Write(ref _maxFrames, 0);
    }

    private static TimeSpan FromTicks(double ticks) => TimeSpan.FromSeconds(ticks / Stopwatch.Frequency);
}
