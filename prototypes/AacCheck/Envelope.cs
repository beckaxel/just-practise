namespace JustPractise.Prototypes.AacCheck;

/// <summary>
/// Lautstärke-Hüllkurve (RMS über alle Kanäle je 10 ms). Vergleicht zwei Decodierungen derselben Aufnahme, auch bei
/// verschiedener Rate, anderem Codec und leichtem Versatz am Anfang.
/// </summary>
internal sealed class Envelope(int channels, int sampleRate)
{
    private const double WindowSeconds = 0.01; // geht bei 44,1 und 48 kHz in ganzen Frames auf

    private readonly int windowFrames = (int)(sampleRate * WindowSeconds);
    private readonly List<double> values = [];
    private double sum;
    private int frames;

    public float Peak { get; private set; }

    public double Seconds => values.Count * WindowSeconds;

    public void Add(ReadOnlySpan<float> samples)
    {
        for (var i = 0; i + channels <= samples.Length; i += channels)
        {
            for (var c = 0; c < channels; c++)
            {
                var sample = samples[i + c];
                sum += sample * (double)sample;
                Peak = Math.Max(Peak, Math.Abs(sample));
            }

            if (++frames == windowFrames)
            {
                values.Add(Math.Sqrt(sum / (frames * channels)));
                sum = 0;
                frames = 0;
            }
        }
    }

    /// <summary>Beste Korrelation bis ±2 s Versatz, der Versatz in ms (positiv: other beginnt später) und der Pegelunterschied in dB.</summary>
    public (double Correlation, double OffsetMs, double LevelDb) Compare(Envelope other)
    {
        const int MaxLag = (int)(2 / WindowSeconds);
        var best = (Correlation: double.MinValue, Lag: 0);
        for (var lag = -MaxLag; lag <= MaxLag; lag++)
        {
            var correlation = Correlate(values, other.values, lag);
            if (correlation > best.Correlation)
            {
                best = (correlation, lag);
            }
        }

        var level = 20 * Math.Log10(Rms(other.values) / Rms(values));
        return (best.Correlation, best.Lag * WindowSeconds * 1000, level);
    }

    private static double Correlate(List<double> a, List<double> b, int lag)
    {
        var start = Math.Max(0, -lag);
        var end = Math.Min(a.Count, b.Count - lag);
        if (end - start < 50)
        {
            return double.MinValue;
        }

        double meanA = 0, meanB = 0;
        for (var i = start; i < end; i++)
        {
            meanA += a[i];
            meanB += b[i + lag];
        }

        meanA /= end - start;
        meanB /= end - start;
        double cov = 0, varA = 0, varB = 0;
        for (var i = start; i < end; i++)
        {
            var x = a[i] - meanA;
            var y = b[i + lag] - meanB;
            cov += x * y;
            varA += x * x;
            varB += y * y;
        }

        return cov / Math.Sqrt(varA * varB);
    }

    private static double Rms(List<double> windows) => Math.Sqrt(windows.Sum(value => value * value) / windows.Count);
}
