using JustPractise.Audio.Engine;
using SoundFlow.Abstracts;
using SoundFlow.Metadata.Models;
using SoundFlow.Providers;

namespace JustPractise.Audio.Player;

/// <summary>
/// Decodiert eine Audiodatei ganz in Blöcke aus dem Pool (Ergebnis von Prüfpunkt 7).
/// </summary>
/// <remarks>
/// Blockiert und gehört auf einen Hintergrund-Thread. Die Tags werden nicht gelesen: Sonst erkennt SoundFlow
/// Dateien mit ID3v2.2 nicht als MP3 und decodiert mit falscher Rate.
/// </remarks>
public sealed class FileDecoder(AudioEngine engine) : IAudioDecoder
{
    private const int ChunkSamples = 16384;

    /// <summary>
    /// Decodiert <paramref name="path"/>. Sobald Kanalzahl und Rate bekannt sind, kommt das noch leere
    /// <see cref="DecodedAudio"/> über <paramref name="opened"/>; es füllt sich danach, während die Wiedergabe schon läuft.
    /// Bei Abbruch oder Fehler gehört das Objekt trotzdem dem Aufrufer, der es mit <see cref="DecodedAudio.Release"/> freigibt.
    /// </summary>
    /// <exception cref="InvalidDataException">Die Datei ist kein lesbares Audio.</exception>
    public void Decode(string path, BlockPool pool, Action<DecodedAudio> opened, CancellationToken cancellationToken)
    {
        using var stream = File.OpenRead(path);
        StreamDataProvider provider;
        try
        {
            provider = new StreamDataProvider(engine, stream, new ReadOptions { ReadTags = false, ReadAlbumArt = false });
        }
        catch (Exception exception) when (exception is not OperationCanceledException and not IOException)
        {
            throw new InvalidDataException($"{path} is not a readable audio file.", exception);
        }

        using (provider)
        {
            var channels = provider.FormatInfo?.ChannelCount ?? 0;
            if (channels <= 0 || provider.SampleRate <= 0)
            {
                throw new InvalidDataException($"{path} has no usable audio format.");
            }

            var audio = new DecodedAudio(pool, channels, provider.SampleRate);
            opened(audio);
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var tail = audio.WritableTail(ChunkSamples);
                if (tail.IsEmpty)
                {
                    break;
                }

                var read = provider.ReadBytes(tail);
                if (read <= 0)
                {
                    break;
                }

                audio.Commit(read);
            }

            audio.Complete();
        }
    }
}
