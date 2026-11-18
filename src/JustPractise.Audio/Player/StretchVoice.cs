namespace JustPractise.Audio.Player;

/// <summary>
/// Time-Stretching mit signalsmith-stretch (Prüfpunkt 3, Standard-Einstellung).
/// </summary>
/// <remarks>
/// Anlegen legt nativen Speicher an und gehört nicht auf den Audio-Thread. Danach legen
/// <see cref="OutputSeek"/> und <see cref="Process"/> keinen Speicher an.
/// </remarks>
public sealed class StretchVoice : IDisposable
{
    private readonly StretchHandle _handle;

    public StretchVoice(int channels, int sampleRate)
    {
        if (!IsAvailable(out var reason))
        {
            throw new InvalidOperationException(reason);
        }

        _handle = StretchNativeMethods.Create(channels, sampleRate, options: 0);
        if (_handle.IsInvalid)
        {
            throw new InvalidOperationException($"jp_stretch_create failed for {channels} channels at {sampleRate} Hz.");
        }

        Channels = channels;
    }

    public int Channels { get; }

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

    /// <summary>Wie viele Frames ab der Zielposition <see cref="OutputSeek"/> bei diesem Tempo braucht.</summary>
    public int OutputSeekLength(double tempo) => StretchNativeMethods.OutputSeekLength(_handle, tempo);

    /// <summary>Springt so, dass die nächste Ausgabe genau am Anfang von <paramref name="input"/> beginnt.</summary>
    public void OutputSeek(ReadOnlySpan<float> input, int frames) => StretchNativeMethods.OutputSeek(_handle, input, frames);

    /// <summary>Dehnt <paramref name="inputFrames"/> auf <paramref name="outputFrames"/>; das Verhältnis ist das Tempo.</summary>
    public void Process(ReadOnlySpan<float> input, int inputFrames, Span<float> output, int outputFrames) =>
        StretchNativeMethods.Process(_handle, input, inputFrames, output, outputFrames);

    public void Dispose() => _handle.Dispose();
}
