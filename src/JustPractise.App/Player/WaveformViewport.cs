namespace JustPractise.App.Player;

/// <summary>
/// Der sichtbare Ausschnitt der Wellenform in Sekunden: ab <see cref="Start"/> für <see cref="Length"/>, innerhalb
/// eines Stücks von <see cref="Duration"/>. Jede Änderung liefert einen gültigen Ausschnitt.
/// </summary>
public readonly record struct WaveformViewport
{
    /// <summary>So weit lässt sich hineinzoomen: eine halbe Sekunde über die ganze Breite.</summary>
    public const double MinLength = 0.5;

    private WaveformViewport(double duration, double start, double length)
    {
        Duration = Math.Max(0, duration);
        Length = Math.Clamp(length, Math.Min(MinLength, Duration), Duration);
        Start = Math.Clamp(start, 0, Duration - Length);
    }

    public double Duration { get; }

    public double Start { get; }

    public double Length { get; }

    public double End => Start + Length;

    public bool IsWhole => Length >= Duration;

    public bool CanZoomIn => Length > Math.Min(MinLength, Duration);

    /// <summary>Das ganze Stück.</summary>
    public static WaveformViewport Whole(double duration) => new(duration, 0, duration);

    /// <summary>Ein Ausschnitt; was über das Stück hinausgeht, wird hineingeschoben.</summary>
    public static WaveformViewport Of(double duration, double start, double length) => new(duration, start, length);

    public bool Contains(double seconds) => seconds >= Start && seconds <= End;

    /// <summary>
    /// Zoomt um <paramref name="factor"/> (über 1 hinein, unter 1 heraus). <paramref name="anchor"/> bleibt an derselben
    /// Stelle im Ausschnitt.
    /// </summary>
    public WaveformViewport ZoomAround(double factor, double anchor)
    {
        var length = Length / factor;
        var fraction = Length > 0 ? (anchor - Start) / Length : 0;
        return new(Duration, anchor - (fraction * length), length);
    }

    /// <summary>
    /// Zoomt um <paramref name="factor"/> um die Abspielposition: Ist sie zu sehen, bleibt sie an ihrer Stelle,
    /// sonst kommt sie in die Mitte.
    /// </summary>
    public WaveformViewport ZoomAtPosition(double factor, double position) =>
        Contains(position) ? ZoomAround(factor, position) : new WaveformViewport(Duration, position - (Length / factor / 2), Length / factor);

    /// <summary>Ein Ausschnitt mit <paramref name="length"/>, in dem <paramref name="seconds"/> bei <paramref name="fraction"/> der Breite liegt.</summary>
    public WaveformViewport Place(double seconds, double fraction, double length) => new(Duration, seconds - (fraction * length), length);

    public WaveformViewport WithStart(double start) => new(Duration, start, Length);

    public WaveformViewport CenterOn(double seconds) => new(Duration, seconds - (Length / 2), Length);

    /// <summary>
    /// Holt <paramref name="position"/> in den Ausschnitt: Ist sie rechts hinausgelaufen, eine Seite weiter, sonst so,
    /// dass sie kurz hinter dem linken Rand liegt.
    /// </summary>
    public WaveformViewport PageTo(double position)
    {
        if (Contains(position))
        {
            return this;
        }

        return position > End && position < End + Length
            ? new(Duration, End, Length)
            : new(Duration, position - (Length * 0.1), Length);
    }

    /// <summary>Das Stück ist länger oder kürzer geworden, z. B. beim Decodieren. Das ganze Stück bleibt das ganze Stück.</summary>
    public WaveformViewport WithDuration(double duration) =>
        IsWhole ? Whole(duration) : new(duration, Start, Length);
}
