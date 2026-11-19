using JustPractise.Core.Pieces;

namespace JustPractise.Audio.Player;

/// <summary>
/// Time-Stretching mit signalsmith-stretch (Prüfpunkt 3, Standard-Einstellung). Rechnet zugleich von der Rate
/// der Datei auf die des Geräts um und gleicht die Tonhöhe dabei aus.
/// </summary>
/// <remarks>
/// <para>
/// „Rate“ ist das Verhältnis Eingabe-Frames zu Ausgabe-Frames: Tempo mal <see cref="RateRatio"/>. Bei 44,1 kHz
/// Datei und 48 kHz Gerät liest der Stretcher bei Tempo 1 also 0,91875 Frames der Datei pro Ausgabe-Frame.
/// </para>
/// <para>
/// Anlegen legt nativen Speicher an und gehört nicht auf den Audio-Thread. Danach legen
/// <see cref="OutputSeek"/> und <see cref="Process"/> bis zum höchsten Tempo keinen Speicher an.
/// </para>
/// </remarks>
public sealed class StretchVoice : IDisposable
{
    private readonly StretchHandle _handle;

    public StretchVoice(int channels, int inputRate, int outputRate)
    {
        if (!IsAvailable(out var reason))
        {
            throw new InvalidOperationException(reason);
        }

        RateRatio = (double)inputRate / outputRate;
        _handle = StretchNativeMethods.Create(channels, inputRate, outputRate, (float)MaxRate, options: 0);
        if (_handle.IsInvalid)
        {
            throw new InvalidOperationException($"jp_stretch_create failed for {channels} channels from {inputRate} Hz to {outputRate} Hz.");
        }

        Channels = channels;
        OutputRate = outputRate;
    }

    public int Channels { get; }

    /// <summary>Die Rate des Geräts, mit der die Ausgabe gespielt wird.</summary>
    public int OutputRate { get; }

    /// <summary>Frames der Datei pro Ausgabe-Frame bei Tempo 1.</summary>
    public double RateRatio { get; }

    /// <summary>Das größte Verhältnis, das beim höchsten Tempo vorkommt.</summary>
    public double MaxRate => FileSettings.MaxTempo * RateRatio;

    /// <summary>Prüft, ob die native Bibliothek da ist und zur Schnittstelle passt.</summary>
    public static bool IsAvailable(out string reason)
    {
        try
        {
            var version = StretchNativeMethods.GetApiVersion();
            reason = version == StretchNativeMethods.ApiVersion
                ? string.Empty
                : $"The native library jpstretch has API version {version}, expected {StretchNativeMethods.ApiVersion}. Rebuild it with: dotnet run tools/setup.cs -- --native";
            return version == StretchNativeMethods.ApiVersion;
        }
        catch (Exception exception) when (exception is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException)
        {
            reason = $"The native library jpstretch is missing ({exception.Message}). Build it with: dotnet run tools/setup.cs";
            return false;
        }
    }

    /// <summary>Wie viele Frames ab der Zielposition <see cref="OutputSeek"/> bei diesem Verhältnis braucht.</summary>
    public int OutputSeekLength(double rate) => StretchNativeMethods.OutputSeekLength(_handle, rate);

    /// <summary>Springt so, dass die nächste Ausgabe genau am Anfang von <paramref name="input"/> beginnt.</summary>
    public void OutputSeek(ReadOnlySpan<float> input, int frames) => StretchNativeMethods.OutputSeek(_handle, input, frames);

    /// <summary>Dehnt <paramref name="inputFrames"/> auf <paramref name="outputFrames"/>; das Verhältnis ist die Rate.</summary>
    public void Process(ReadOnlySpan<float> input, int inputFrames, Span<float> output, int outputFrames) =>
        StretchNativeMethods.Process(_handle, input, inputFrames, output, outputFrames);

    public void Dispose() => _handle.Dispose();
}
