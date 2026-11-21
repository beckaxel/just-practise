using System.Diagnostics;
using SoundFlow.Backends.MiniAudio;

namespace JustPractise.Prototypes.DecodeCheck;

/// <summary>Ein Sprung beim Streamen und wie genau er gegenüber dem ganz decodierten Signal landet.</summary>
internal sealed record SeekResult(
    double TargetSeconds,
    double SeekMilliseconds,
    double FirstBlockMilliseconds,
    long ReportedOffsetFrames,
    long? OffsetFrames,
    double MaxDifference,
    double MaxDifferenceStart);

/// <summary>
/// Springt mit dem streamenden Decoder an verschiedene Stellen, in der Reihenfolge wie beim Üben
/// (vor, zurück zum Loop-Anfang, ans Ende), und vergleicht das Gelesene mit dem ganz decodierten Signal.
/// </summary>
internal static class SeekCheck
{
    private static readonly double[] Fractions = [0.5, 0.9, 0.5, 0.1, 0.99, 0.25, 0.75];

    /// <summary>Verglichen wird ab diesem Frame nach dem Sprung, davor liegt die Einschwingzeit des Decoders.</summary>
    private const int CompareStartFrames = 2048;

    private const int CompareFrames = 4096;

    public static SeekResult[] Run(MiniAudioEngine engine, string path, float[] reference, long frames, int channels, int rate)
    {
        using var stream = File.OpenRead(path);
        using var provider = FullDecoder.Open(engine, stream);
        var blockFrames = rate / 100;
        var window = new float[(CompareStartFrames + CompareFrames) * channels];
        var block = new float[blockFrames * channels];

        // Aufwärmen, damit JIT nicht in den ersten Sprung fällt
        provider.Seek(0);
        provider.ReadBytes(block);

        var results = new List<SeekResult>();
        foreach (var fraction in Fractions)
        {
            var targetFrame = (long)(frames * fraction);
            var t0 = Stopwatch.GetTimestamp();
            provider.Seek((int)(targetFrame * channels));
            var seekMs = Stopwatch.GetElapsedTime(t0).TotalMilliseconds;
            var reported = (provider.Position / channels) - targetFrame;

            var t1 = Stopwatch.GetTimestamp();
            var firstRead = provider.ReadBytes(block);
            var firstBlockMs = Stopwatch.GetElapsedTime(t1).TotalMilliseconds;

            // Fenster ab dem Ziel: der erste Block plus weitere Daten
            block.AsSpan(0, firstRead).CopyTo(window);
            var filled = firstRead;
            while (filled < window.Length)
            {
                var n = provider.ReadBytes(window.AsSpan(filled));
                if (n <= 0)
                {
                    break;
                }

                filled += n;
            }

            var (offset, maxDiff, maxDiffStart) = Align(reference, frames, channels, rate, targetFrame, window.AsSpan(0, filled));
            results.Add(new SeekResult((double)targetFrame / rate, seekMs, firstBlockMs, reported, offset, maxDiff, maxDiffStart));
        }

        return [.. results];
    }

    /// <summary>
    /// Sucht den Versatz (in Frames) zwischen dem gestreamten Fenster und der Referenz, bei dem Kanal 0 am besten passt.
    /// Erst eng um 0, dann bis ±0,5 s. Liefert null, wenn nichts deckungsgleich ist.
    /// </summary>
    private static (long? Offset, double MaxDiff, double MaxDiffStart) Align(
        float[] reference, long frames, int channels, int rate, long targetFrame, ReadOnlySpan<float> window)
    {
        var windowFrames = window.Length / channels;
        if (windowFrames < CompareStartFrames + CompareFrames)
        {
            return (null, double.NaN, double.NaN);
        }

        var best = Search(reference, frames, channels, targetFrame, window, 64);
        if (best.Error > 1e-3)
        {
            var wide = Search(reference, frames, channels, targetFrame, window, rate / 2);
            if (wide.Error < best.Error)
            {
                best = wide;
            }
        }

        if (best.Error > 1e-3)
        {
            return (null, best.Error, double.NaN);
        }

        // Größte Abweichung über alle Kanäle: im Vergleichsfenster und in der Einschwingzeit davor
        double maxDiff = 0, maxStart = 0;
        for (var f = 0; f < CompareStartFrames + CompareFrames; f++)
        {
            var refFrame = targetFrame + best.Offset + f;
            if (refFrame < 0 || refFrame >= frames)
            {
                continue;
            }

            for (var c = 0; c < channels; c++)
            {
                var diff = Math.Abs(window[(f * channels) + c] - reference[(refFrame * channels) + c]);
                if (f < CompareStartFrames)
                {
                    maxStart = Math.Max(maxStart, diff);
                }
                else
                {
                    maxDiff = Math.Max(maxDiff, diff);
                }
            }
        }

        return (best.Offset, maxDiff, maxStart);
    }

    /// <summary>Mittlere absolute Abweichung von Kanal 0 für den besten Versatz im Bereich ±<paramref name="range"/>.</summary>
    private static (long Offset, double Error) Search(
        float[] reference, long frames, int channels, long targetFrame, ReadOnlySpan<float> window, int range)
    {
        var bestOffset = 0L;
        var bestError = double.MaxValue;
        for (long d = -range; d <= range; d++)
        {
            var refStart = targetFrame + d + CompareStartFrames;
            if (refStart < 0 || refStart + CompareFrames > frames)
            {
                continue;
            }

            var limit = bestError * CompareFrames;
            double sum = 0;
            for (var i = 0; i < CompareFrames && sum < limit; i++)
            {
                sum += Math.Abs(window[(CompareStartFrames + i) * channels] - reference[(refStart + i) * channels]);
            }

            var error = sum / CompareFrames;
            if (error < bestError)
            {
                bestError = error;
                bestOffset = d;
            }
        }

        return (bestOffset, bestError);
    }
}
