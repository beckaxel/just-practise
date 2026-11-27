namespace JustPractise.Core.Waveform;

/// <summary>
/// Die Spitzen einer Datei für die Wellenform: pro Abschnitt der kleinste und größte Wert über alle Kanäle,
/// in mehreren Stufen von <see cref="BaseFrames"/> Frames an, jede Stufe <see cref="LevelFactor"/>-mal gröber.
/// </summary>
/// <remarks>
/// Alle Stufen liegen in einem einzigen Array mit 16-Bit-Werten (Regeln für die Speicherbereinigung: wenige große
/// Arrays). 15 Minuten bei 48 kHz brauchen rund 3,6 MB. Die feinste Stufe reicht bis etwa eine halbe Sekunde über die
/// ganze Breite. Nach dem Anlegen unveränderlich und daher von jedem Thread lesbar.
/// </remarks>
public sealed class WaveformPeaks
{
    public const int BaseFrames = 64;
    public const int LevelFactor = 4;

    private readonly short[] _data;
    private readonly int[] _levelStart;
    private readonly int[] _levelCount;

    internal WaveformPeaks(short[] data, int[] levelStart, int[] levelCount, long frames, int sampleRate)
    {
        _data = data;
        _levelStart = levelStart;
        _levelCount = levelCount;
        Frames = frames;
        SampleRate = sampleRate;
    }

    public long Frames { get; }

    public int SampleRate { get; }

    public int Levels => _levelCount.Length;

    public TimeSpan Duration => TimeSpan.FromSeconds((double)Frames / SampleRate);

    /// <summary>
    /// Füllt pro Spalte den kleinsten und größten Wert (−1 bis 1) für die Frames ab <paramref name="startFrame"/>,
    /// je <paramref name="framesPerColumn"/> pro Spalte. Spalten außerhalb der Datei bleiben bei 0.
    /// </summary>
    public void Envelope(double startFrame, double framesPerColumn, Span<float> min, Span<float> max)
    {
        if (min.Length != max.Length)
        {
            throw new ArgumentException("min and max must have the same length.", nameof(max));
        }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(framesPerColumn);

        var level = 0;
        long bucketFrames = BaseFrames;
        while (level + 1 < Levels && bucketFrames * LevelFactor <= framesPerColumn)
        {
            level++;
            bucketFrames *= LevelFactor;
        }

        var start = _levelStart[level];
        var count = _levelCount[level];
        for (var column = 0; column < min.Length; column++)
        {
            var from = startFrame + (column * framesPerColumn);
            var to = Math.Min(from + framesPerColumn, Frames);
            from = Math.Max(from, 0);
            if (from >= to || count == 0)
            {
                min[column] = 0;
                max[column] = 0;
                continue;
            }

            var first = (int)Math.Min((long)(from / bucketFrames), count - 1);
            var last = (int)Math.Min((long)Math.Ceiling(to / bucketFrames) - 1, count - 1);
            var low = short.MaxValue;
            var high = short.MinValue;
            for (var bucket = first; bucket <= Math.Max(first, last); bucket++)
            {
                var index = (start + bucket) * 2;
                low = Math.Min(low, _data[index]);
                high = Math.Max(high, _data[index + 1]);
            }

            min[column] = low / (float)short.MaxValue;
            max[column] = high / (float)short.MaxValue;
        }
    }
}
