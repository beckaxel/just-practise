using JustPractise.Audio.Engine;

namespace JustPractise.Audio.Player;

/// <summary>
/// Decodiert M4A (AAC, ALAC) und rohes AAC über das mitgelieferte, schlanke FFmpeg (<c>jpaac</c>, Prüfpunkt 4) ganz in
/// Blöcke aus dem Pool, wie <see cref="FileDecoder"/>.
/// </summary>
/// <remarks>Blockiert und gehört auf einen Hintergrund-Thread.</remarks>
public sealed class AacDecoder : IAudioDecoder
{
    private const int ChunkSamples = 16384;

    /// <summary>Prüft, ob die native Bibliothek da ist und zur Schnittstelle passt.</summary>
    public static bool IsAvailable(out string reason)
    {
        try
        {
            var version = AacNativeMethods.GetApiVersion();
            reason = version == AacNativeMethods.ApiVersion
                ? string.Empty
                : $"The native library jpaac has API version {version}, expected {AacNativeMethods.ApiVersion}. Rebuild it with: dotnet run tools/setup.cs -- --native";
            return version == AacNativeMethods.ApiVersion;
        }
        catch (Exception exception) when (exception is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException)
        {
            reason = $"The native library jpaac is missing ({exception.Message}). Build it with: dotnet run tools/setup.cs";
            return false;
        }
    }

    /// <inheritdoc cref="FileDecoder.Decode"/>
    public void Decode(string path, BlockPool pool, Action<DecodedAudio> opened, CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"{path} does not exist.", path);
        }

        if (!IsAvailable(out var reason))
        {
            throw new InvalidDataException($"{path} cannot be read: {reason}");
        }

        var error = AacNativeMethods.Open(path, out var handle);
        using (handle)
        {
            if (error != 0)
            {
                throw new InvalidDataException($"{path} is not a readable M4A or AAC file (jp_aac_open: {error}).");
            }

            var channels = AacNativeMethods.Channels(handle);
            var audio = new DecodedAudio(pool, channels, AacNativeMethods.SampleRate(handle));
            opened(audio);
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var tail = audio.WritableTail(ChunkSamples);
                if (tail.IsEmpty)
                {
                    break;
                }

                // Ein Fehler mitten in der Datei beendet das Decodieren; was bis dahin da ist, bleibt abspielbar.
                var frames = AacNativeMethods.Read(handle, tail, tail.Length / channels);
                if (frames <= 0)
                {
                    break;
                }

                audio.Commit(frames * channels);
            }

            audio.Complete();
        }
    }
}
