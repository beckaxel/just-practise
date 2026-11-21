namespace JustPractise.Audio.Metronome;

/// <summary>
/// Der Klang eines Klicks, einmal vorab berechnet: ein kurzer, abklingender Sinus.
/// Die Eins ist höher und lauter als die anderen Schläge.
/// </summary>
public sealed class ClickSound
{
    private const double Seconds = 0.025;
    private const double Decay = 200;

    private ClickSound(float[] accented, float[] normal)
    {
        Accented = accented;
        Normal = normal;
    }

    public ReadOnlyMemory<float> Accented { get; }

    public ReadOnlyMemory<float> Normal { get; }

    public static ClickSound Create(int sampleRate) =>
        new(Render(sampleRate, 1600, 0.6), Render(sampleRate, 1000, 0.4));

    private static float[] Render(int sampleRate, double frequency, double amplitude)
    {
        var samples = new float[(int)(Seconds * sampleRate)];
        for (var i = 0; i < samples.Length; i++)
        {
            var t = (double)i / sampleRate;
            samples[i] = (float)(Math.Sin(2 * Math.PI * frequency * t) * Math.Exp(-t * Decay) * amplitude);
        }

        return samples;
    }
}
