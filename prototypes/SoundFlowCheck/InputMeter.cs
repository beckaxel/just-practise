using System.Diagnostics;
using SoundFlow.Enums;

namespace JustPractise.Prototypes.SoundFlowCheck;

/// <summary>
/// Pegel des Eingabegeräts und Erkennung des Messklicks für die Latenzmessung.
/// Läuft auf dem Audio-Thread: keine Allokationen, keine Locks.
/// </summary>
internal sealed class InputMeter(int channels, int sampleRate)
{
    private volatile float _peak;
    private volatile float _rms;
    private float _peakHold;

    private volatile bool _armed;
    private float _threshold;

    public float PeakDb => ToDb(_peak);

    public float RmsDb => ToDb(_rms);

    /// <summary>Zeitpunkt (Stopwatch-Ticks), zu dem der Messklick am Eingang ankam, sonst 0.</summary>
    public long DetectedTimestamp { get; private set; }

    public void Arm()
    {
        _threshold = Math.Max(0.02f, _peakHold * 4);
        DetectedTimestamp = 0;
        _armed = true;
    }

    public void OnAudioProcessed(Span<float> samples, Capability capability)
    {
        var now = Stopwatch.GetTimestamp();
        var peak = 0f;
        var sum = 0.0;
        var detectedAt = -1;

        for (var i = 0; i < samples.Length; i++)
        {
            var a = Math.Abs(samples[i]);
            peak = Math.Max(peak, a);
            sum += a * a;
            if (_armed && detectedAt < 0 && a > _threshold)
            {
                detectedAt = i / channels;
            }
        }

        if (detectedAt >= 0)
        {
            var framesAfter = (samples.Length / channels) - detectedAt;
            DetectedTimestamp = now - (long)((double)framesAfter / sampleRate * Stopwatch.Frequency);
            _armed = false;
        }

        _peak = peak;
        _rms = (float)Math.Sqrt(sum / Math.Max(1, samples.Length));
        _peakHold = Math.Max(peak, _peakHold * 0.9f);
    }

    private static float ToDb(float value) => value <= 1e-6f ? -120f : 20f * MathF.Log10(value);
}
