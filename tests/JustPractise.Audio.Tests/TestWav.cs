using System.Text;

namespace JustPractise.Audio.Tests;

/// <summary>Schreibt 16-Bit-WAV-Dateien für Tests.</summary>
internal static class TestWav
{
    /// <summary>Frame n, Kanal c hat den Wert (n % 1000 + 100 c) / 32768.</summary>
    public static string Write(string path, int channels, int sampleRate, int frames)
    {
        using var writer = new BinaryWriter(File.Create(path));
        var dataBytes = frames * channels * 2;
        writer.Write(Encoding.ASCII.GetBytes("RIFF"));
        writer.Write(36 + dataBytes);
        writer.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
        writer.Write(16);
        writer.Write((short)1);
        writer.Write((short)channels);
        writer.Write(sampleRate);
        writer.Write(sampleRate * channels * 2);
        writer.Write((short)(channels * 2));
        writer.Write((short)16);
        writer.Write(Encoding.ASCII.GetBytes("data"));
        writer.Write(dataBytes);
        for (var frame = 0; frame < frames; frame++)
        {
            for (var channel = 0; channel < channels; channel++)
            {
                writer.Write((short)((frame % 1000) + (100 * channel)));
            }
        }

        return path;
    }

    public static float Expected(long frame, int channel) => ((frame % 1000) + (100 * channel)) / 32768f;
}
