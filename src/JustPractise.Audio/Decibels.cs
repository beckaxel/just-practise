namespace JustPractise.Audio;

public static class Decibels
{
    /// <summary>Rechnet eine Verstärkung in dB in einen Faktor um, 0 dB = 1, −6 dB ≈ 0,5.</summary>
    public static float ToGain(double decibels) => (float)Math.Pow(10, decibels / 20);
}
