namespace JustPractise.Audio.Tuner;

/// <summary>Das Ergebnis einer Messung: Frequenz (0 ohne erkannten Ton), Klarheit und Pegel.</summary>
/// <param name="Frequency">die Grundfrequenz in Hz, 0 wenn kein Ton erkannt wurde</param>
/// <param name="Clarity">wie periodisch das Signal ist, 0 bis 1; ein klarer Ton liegt über 0,9</param>
/// <param name="LevelDb">der Pegel (RMS) in dBFS</param>
public readonly record struct PitchReading(double Frequency, double Clarity, double LevelDb)
{
    public bool HasPitch => Frequency > 0;
}

/// <summary>
/// Erkennt die Grundfrequenz nach dem McLeod-Verfahren (MPM, „A Smarter Way to Find Pitch“, 2005): normierte
/// Autokorrelation (NSDF) über eine FFT, dann der erste Gipfel, der fast so hoch ist wie der höchste. Das vermeidet
/// Oktavfehler bei obertonreichen Instrumenten. Parabolische Interpolation macht die Frequenz genauer als ein Sample.
/// </summary>
/// <remarks>
/// Läuft nicht auf dem Audio-Thread, legt aber trotzdem beim Messen nichts an (Puffer aus dem Konstruktor).
/// Nicht threadsicher: eine Instanz pro Thread.
/// </remarks>
public sealed class PitchDetector
{
    /// <summary>Tiefster erkannter Ton: knapp unter dem A0 (27,5 Hz), damit auch ein tiefes E1 (41 Hz) sicher geht.</summary>
    public const double MinFrequency = 25;

    /// <summary>Höchster erkannter Ton: etwas über C8 (4186 Hz).</summary>
    public const double MaxFrequency = 4500;

    /// <summary>Ein Gipfel zählt, wenn er mindestens so hoch ist; der Wert aus dem Aufsatz.</summary>
    private const double PeakThreshold = 0.93;

    private readonly int _sampleRate;
    private readonly int _window;
    private readonly int _minLag;
    private readonly int _maxLag;
    private readonly Fft _fft;
    private readonly double[] _real;
    private readonly double[] _imaginary;
    private readonly double[] _nsdf;

    /// <param name="sampleRate">Abtastrate in Hz</param>
    /// <param name="window">Fensterlänge in Samples; sie muss mindestens zwei Perioden des tiefsten Tons fassen</param>
    public PitchDetector(int sampleRate, int window)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sampleRate);
        _sampleRate = sampleRate;
        _window = window;
        _minLag = Math.Max(2, (int)Math.Floor(sampleRate / MaxFrequency));
        _maxLag = Math.Min(window / 2, (int)Math.Ceiling(sampleRate / MinFrequency));
        var size = (int)System.Numerics.BitOperations.RoundUpToPowerOf2((uint)(window * 2));
        _fft = new Fft(size);
        _real = new double[size];
        _imaginary = new double[size];
        _nsdf = new double[_maxLag + 2];
    }

    public int Window => _window;

    /// <summary>Wie sicher ein Ton sein muss, damit er gilt (0 bis 1).</summary>
    public double MinClarity { get; init; } = 0.85;

    /// <summary>Leiser als das (dBFS) gilt als Stille, z. B. das Rauschen des Raums.</summary>
    public double MinLevelDb { get; init; } = -60;

    /// <summary>Misst die letzten <see cref="Window"/> Samples (mono) von <paramref name="samples"/>.</summary>
    public PitchReading Detect(ReadOnlySpan<float> samples)
    {
        if (samples.Length < _window)
        {
            throw new ArgumentException($"Es werden mindestens {_window} Samples gebraucht.", nameof(samples));
        }

        var x = samples[^_window..];
        var energy = 0.0;
        for (var i = 0; i < _window; i++)
        {
            energy += (double)x[i] * x[i];
        }

        var levelDb = 10 * Math.Log10(Math.Max(energy / _window, 1e-20));
        if (levelDb < MinLevelDb)
        {
            return new PitchReading(0, 0, levelDb);
        }

        Autocorrelate(x);
        Normalize(x, energy);
        var (lag, clarity) = PickPeak();
        if (lag <= 0 || clarity < MinClarity)
        {
            return new PitchReading(0, clarity, levelDb);
        }

        return new PitchReading(_sampleRate / lag, clarity, levelDb);
    }

    /// <summary>r(τ) = Σ x(j)·x(j+τ) über die FFT: Betragsquadrat des Spektrums, zurücktransformiert.</summary>
    private void Autocorrelate(ReadOnlySpan<float> x)
    {
        Array.Clear(_real);
        Array.Clear(_imaginary);
        for (var i = 0; i < _window; i++)
        {
            _real[i] = x[i];
        }

        _fft.Transform(_real, _imaginary, inverse: false);
        for (var i = 0; i < _real.Length; i++)
        {
            _real[i] = (_real[i] * _real[i]) + (_imaginary[i] * _imaginary[i]);
            _imaginary[i] = 0;
        }

        _fft.Transform(_real, _imaginary, inverse: true);
    }

    /// <summary>n(τ) = 2·r(τ) / m(τ) mit m(τ) = Σ x(j)² + x(j+τ)², schrittweise aus der Energie.</summary>
    private void Normalize(ReadOnlySpan<float> x, double energy)
    {
        var scale = 1.0 / _real.Length; // die inverse FFT normiert nicht
        var m = 2 * energy;
        for (var lag = 0; lag <= _maxLag + 1 && lag < _window; lag++)
        {
            if (lag > 0)
            {
                m -= ((double)x[lag - 1] * x[lag - 1]) + ((double)x[_window - lag] * x[_window - lag]);
            }

            _nsdf[lag] = m > 1e-12 ? 2 * _real[lag] * scale / m : 0;
        }
    }

    /// <summary>
    /// Sucht die Gipfel zwischen den positiven Nulldurchgängen und nimmt den ersten, der mindestens
    /// <see cref="PeakThreshold"/> des höchsten erreicht. Gibt die interpolierte Verzögerung und ihre Höhe zurück.
    /// </summary>
    private (double Lag, double Clarity) PickPeak()
    {
        // Erst nach dem ersten negativen Nulldurchgang suchen; davor liegt der Gipfel bei τ = 0.
        var lag = 1;
        while (lag < _maxLag && _nsdf[lag] > 0)
        {
            lag++;
        }

        var highest = 0.0;
        Span<int> peaks = stackalloc int[64];
        var count = 0;
        while (lag < _maxLag && count < peaks.Length)
        {
            while (lag < _maxLag && _nsdf[lag] <= 0)
            {
                lag++;
            }

            var best = -1;
            while (lag < _maxLag && _nsdf[lag] > 0)
            {
                if (lag >= _minLag && (best < 0 || _nsdf[lag] > _nsdf[best]))
                {
                    best = lag;
                }

                lag++;
            }

            if (best > 0)
            {
                peaks[count++] = best;
                highest = Math.Max(highest, _nsdf[best]);
            }
        }

        foreach (var peak in peaks[..count])
        {
            if (_nsdf[peak] >= PeakThreshold * highest)
            {
                return Interpolate(peak);
            }
        }

        return (0, highest);
    }

    /// <summary>Scheitel der Parabel durch den Gipfel und seine Nachbarn.</summary>
    private (double Lag, double Clarity) Interpolate(int peak)
    {
        var left = _nsdf[peak - 1];
        var middle = _nsdf[peak];
        var right = _nsdf[peak + 1];
        var denominator = left - (2 * middle) + right;
        if (Math.Abs(denominator) < 1e-12)
        {
            return (peak, middle);
        }

        var shift = 0.5 * (left - right) / denominator;
        return (peak + shift, middle - (0.25 * (left - right) * shift));
    }
}
