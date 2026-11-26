using System.Globalization;

namespace JustPractise.Core.Timer;

/// <summary>Der Zustand des Übetimers; jeder hat eine eigene Farbe.</summary>
public enum TimerPhase
{
    /// <summary>Steht auf der eingestellten Dauer und wartet auf Start.</summary>
    Ready,

    Running,

    Paused,

    /// <summary>Läuft in den letzten 30 Sekunden.</summary>
    LastSeconds,

    /// <summary>Bei 0 angekommen; bleibt so bis zum Neustart, wie eine Sanduhr.</summary>
    Finished,
}

/// <summary>
/// Der Übetimer: ein Countdown in ganzen Minuten, nur von Hand bedient und ohne jedes Geräusch. Die Anzeige kennt
/// keine Sekunden außer in den letzten 30 (in 10-Sekunden-Schritten). Die Minuten lassen sich jederzeit neu setzen,
/// auch während er läuft; er läuft dann von dort aus weiter.
/// </summary>
/// <remarks>
/// Die Zeit kommt aus <see cref="TimeProvider"/>, damit Tests sie vorlaufen lassen können. Gemessen wird mit dem
/// monotonen Zeitstempel, eine Umstellung der Uhr ändert also nichts.
/// </remarks>
public sealed class PracticeTimer(TimeProvider time, int minutes)
{
    public static readonly TimeSpan LastSecondsThreshold = TimeSpan.FromSeconds(30);

    private enum State
    {
        Ready,
        Running,
        Paused,
        Finished,
    }

    private State _state = State.Ready;

    /// <summary>Was beim letzten Start, bei der letzten Pause oder beim letzten Setzen übrig war.</summary>
    private TimeSpan _remainingAtMark = TimeSpan.FromMinutes(minutes);

    private long _markedAt;

    /// <summary>Die zuletzt gesetzten Minuten; mit ihnen startet der Timer nach dem Ablaufen neu.</summary>
    public int Minutes { get; private set; } = minutes;

    public TimeSpan Duration => TimeSpan.FromMinutes(Minutes);

    public TimeSpan Remaining
    {
        get
        {
            Update();
            return _state switch
            {
                State.Finished => TimeSpan.Zero,
                State.Running => Max(_remainingAtMark - time.GetElapsedTime(_markedAt), TimeSpan.Zero),
                _ => _remainingAtMark,
            };
        }
    }

    public TimerPhase Phase
    {
        get
        {
            Update();
            return _state switch
            {
                State.Ready => TimerPhase.Ready,
                State.Paused => TimerPhase.Paused,
                State.Finished => TimerPhase.Finished,
                _ => Remaining <= LastSecondsThreshold ? TimerPhase.LastSeconds : TimerPhase.Running,
            };
        }
    }

    public bool IsRunning
    {
        get
        {
            Update();
            return _state == State.Running;
        }
    }

    /// <summary>Wie viel noch übrig ist, von 1 (die zuletzt gesetzten Minuten) bis 0.</summary>
    public double Fraction => Duration > TimeSpan.Zero ? Math.Clamp(Remaining / Duration, 0, 1) : 0;

    /// <summary>Die angezeigten Minuten, aufgerundet; in den letzten 30 Sekunden 1, abgelaufen 0.</summary>
    public int DisplayMinutes => (int)Math.Ceiling(Remaining.TotalMinutes);

    /// <summary>Startet oder setzt nach einer Pause fort; nach dem Ablaufen startet er mit <see cref="Minutes"/> neu.</summary>
    public void Start()
    {
        Update();
        if (_state == State.Running)
        {
            return;
        }

        if (_state == State.Finished)
        {
            _remainingAtMark = Duration;
        }

        _markedAt = time.GetTimestamp();
        _state = State.Running;
    }

    public void Pause()
    {
        if (!IsRunning)
        {
            return;
        }

        _remainingAtMark = Remaining;
        _state = State.Paused;
    }

    /// <summary>Start, wenn er steht oder abgelaufen ist; Pause, wenn er läuft.</summary>
    public void StartOrPause()
    {
        if (IsRunning)
        {
            Pause();
        }
        else
        {
            Start();
        }
    }

    /// <summary>Zurück auf die zuletzt gesetzten Minuten, angehalten.</summary>
    public void Reset()
    {
        _state = State.Ready;
        _remainingAtMark = Duration;
    }

    /// <summary>
    /// Setzt die Restzeit auf <paramref name="minutes"/> ganze Minuten, die auch für einen Neustart gelten. Ein
    /// laufender Timer läuft von dort aus weiter, ein pausierter bleibt pausiert, ein abgelaufener steht wieder bereit.
    /// So lässt er sich jederzeit verkürzen oder verlängern.
    /// </summary>
    public void SetMinutes(int minutes)
    {
        Update();
        Minutes = minutes;
        _remainingAtMark = Duration;
        _markedAt = time.GetTimestamp();
        if (_state == State.Finished)
        {
            _state = State.Ready;
        }
    }

    /// <summary>Die Anzeige, z. B. „25“ (Minuten, aufgerundet) oder in den letzten 30 Sekunden „30“, „20“, „10“.</summary>
    public string Display => Format(Remaining, out _);

    /// <summary>Ob <see cref="Display"/> Sekunden zeigt (letzte 30 Sekunden) statt Minuten.</summary>
    public bool DisplaysSeconds
    {
        get
        {
            Format(Remaining, out var seconds);
            return seconds;
        }
    }

    /// <summary>
    /// Minuten immer aufgerundet: Direkt nach dem Start von 25 Minuten steht weiter „25“. In den letzten 30 Sekunden
    /// in 10-Sekunden-Schritten, ebenfalls aufgerundet. Abgelaufen „0“.
    /// </summary>
    public static string Format(TimeSpan remaining, out bool seconds)
    {
        seconds = remaining > TimeSpan.Zero && remaining <= LastSecondsThreshold;
        var value = remaining <= TimeSpan.Zero
            ? 0
            : seconds
                ? (int)Math.Ceiling(remaining.TotalSeconds / 10) * 10
                : (int)Math.Ceiling(remaining.TotalMinutes);
        return value.ToString(CultureInfo.InvariantCulture);
    }

    private static TimeSpan Max(TimeSpan a, TimeSpan b) => a > b ? a : b;

    private void Update()
    {
        if (_state == State.Running && _remainingAtMark - time.GetElapsedTime(_markedAt) <= TimeSpan.Zero)
        {
            _state = State.Finished;
        }
    }
}
