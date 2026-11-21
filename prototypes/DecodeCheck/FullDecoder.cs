using System.Diagnostics;
using SoundFlow.Backends.MiniAudio;
using SoundFlow.Metadata.Models;
using SoundFlow.Providers;

namespace JustPractise.Prototypes.DecodeCheck;

/// <summary>Messwerte beim vollständigen Decodieren einer Datei.</summary>
internal sealed record DecodeStats(
    string Storage,
    string Detected,
    int Channels,
    int SampleRate,
    long Frames,
    long EstimatedSamples,
    double OpenMilliseconds,
    double FirstSecondsMilliseconds,
    double TotalMilliseconds,
    int Resizes,
    double BufferMegabytes,
    double AllocatedMegabytes,
    int Gen2Collections,
    int FullBlocking,
    double MaxPauseMilliseconds)
{
    public double Seconds => (double)Frames / SampleRate;

    public double RealtimeFactor => Seconds / (TotalMilliseconds / 1000);
}

/// <summary>
/// Decodiert eine Datei ganz in ein einziges großes Array, wie es die App im Hintergrund tun würde.
/// Das Array wird anhand der geschätzten Länge vorab angelegt und nur bei Bedarf vergrößert.
/// Gültig sind nur die ersten <see cref="DecodeStats.Frames"/> Frames.
/// </summary>
internal static class FullDecoder
{
    /// <summary>Ab so vielen decodierten Sekunden könnte die Wiedergabe beginnen.</summary>
    public const double StartSeconds = 5;

    private const int ChunkSamples = 16384;

    public static (DecodeStats Stats, float[] Samples) DecodeFloat(MiniAudioEngine engine, string path)
    {
        float[] samples = [];
        var stats = Decode(path, "float (32 Bit)", sizeof(float), stream => OpenProvider(engine, stream, readTags: false),
            capacity => (samples = GC.AllocateUninitializedArray<float>(capacity)).Length,
            capacity => Array.Resize(ref samples, capacity),
            (offset, source) => source.Read(samples.AsSpan(offset, Math.Min(ChunkSamples, samples.Length - offset))));
        return (stats, samples);
    }

    public static (DecodeStats Stats, short[] Samples) DecodeInt16(MiniAudioEngine engine, string path)
    {
        short[] samples = [];
        var chunk = new float[ChunkSamples];
        var stats = Decode(path, "short (16 Bit)", sizeof(short), stream => OpenProvider(engine, stream, readTags: false),
            capacity => (samples = GC.AllocateUninitializedArray<short>(capacity)).Length,
            capacity => Array.Resize(ref samples, capacity),
            (offset, source) =>
            {
                var read = source.Read(chunk.AsSpan(0, Math.Min(ChunkSamples, samples.Length - offset)));
                var target = samples.AsSpan(offset, read);
                for (var i = 0; i < read; i++)
                {
                    target[i] = (short)Math.Clamp(MathF.Round(chunk[i] * 32767f), -32768f, 32767f);
                }

                return read;
            });
        return (stats, samples);
    }

    /// <summary>
    /// Wie <see cref="DecodeFloat"/>, aber mit dem Lesen der Tags. Bei ID3v2.2 (ältere iTunes-Dateien) scheitert dann
    /// die Formaterkennung von SoundFlow, und es decodiert mit 48 kHz statt der Originalrate.
    /// </summary>
    public static (DecodeStats Stats, float[] Samples) DecodeFloatWithTags(MiniAudioEngine engine, string path)
    {
        float[] samples = [];
        var stats = Decode(path, "float, mit Tags lesen", sizeof(float), stream => OpenProvider(engine, stream, readTags: true),
            capacity => (samples = GC.AllocateUninitializedArray<float>(capacity)).Length,
            capacity => Array.Resize(ref samples, capacity),
            (offset, source) => source.Read(samples.AsSpan(offset, Math.Min(ChunkSamples, samples.Length - offset))));
        return (stats, samples);
    }

    /// <summary>
    /// Wie <see cref="DecodeFloat"/>, aber in Blöcke aus dem Pool. Die geschätzte Länge wird nicht gebraucht,
    /// beim Wachsen wird nichts kopiert, und die Blöcke der vorigen Datei werden wiederverwendet.
    /// </summary>
    public static (DecodeStats Stats, BlockAudio Audio) DecodeBlocks(MiniAudioEngine engine, string path, BlockPool pool)
    {
        BlockAudio? audio = null;
        var stats = Decode(path, "float, Blöcke aus Pool", sizeof(float), stream => OpenProvider(engine, stream, readTags: false),
            _ => Array.MaxLength,
            _ => throw new InvalidOperationException("Blöcke wachsen von selbst"),
            (_, source) =>
            {
                audio ??= new BlockAudio(pool, source.Channels);
                var read = source.Read(audio.WritableTail(ChunkSamples));
                audio.Commit(read);
                return read;
            });
        return (stats, audio!);
    }

    /// <summary>Öffnet die Datei so, wie die App es tun soll: ohne Tags, damit die Originalrate erkannt wird.</summary>
    public static StreamDataProvider Open(MiniAudioEngine engine, Stream stream, bool readTags = false) =>
        new(engine, stream, new ReadOptions { ReadTags = readTags });

    private static Source OpenProvider(MiniAudioEngine engine, Stream stream, bool readTags)
    {
        var provider = Open(engine, stream, readTags);
        var info = provider.FormatInfo;
        var detected = info is null
            ? "keine Metadaten"
            : $"Metadaten: {info.FormatName}, {info.SampleRate} Hz, {info.Duration.TotalSeconds:0.0} s";
        return new Source(provider.ReadBytes, info?.ChannelCount ?? 2, provider.SampleRate, provider.Length, detected, provider);
    }

    private static DecodeStats Decode(
        string path,
        string storage,
        int bytesPerSample,
        Func<Stream, Source> open,
        Func<int, int> allocate,
        Action<int> grow,
        Func<int, Source, int> read)
    {
        var gc = new GcMonitor();
        var allocatedBefore = GC.GetTotalAllocatedBytes(precise: true);
        var start = Stopwatch.GetTimestamp();

        using var stream = File.OpenRead(path);
        var source = open(stream);
        using var owner = source.Owner;
        var openMs = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
        var channels = source.Channels;
        var rate = source.SampleRate;
        var estimate = (long)source.Length;

        // Etwas Reserve, weil die Schätzung bei MP3 nicht genau sein muss
        var capacity = estimate > 0 ? estimate + (rate * channels) : 60L * rate * channels;
        var length = allocate(ToCapacity(capacity));

        var count = 0;
        var resizes = 0;
        var firstMs = -1.0;
        var firstTarget = (long)(StartSeconds * rate * channels);
        while (true)
        {
            if (count >= length)
            {
                length = ToCapacity((length * 3L / 2) + ChunkSamples);
                grow(length);
                resizes++;
            }

            var n = read(count, source);
            if (n <= 0)
            {
                break;
            }

            count += n;
            if (firstMs < 0 && count >= firstTarget)
            {
                firstMs = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
            }

            gc.Poll();
        }

        // Die Reserve am Ende bleibt stehen, Abschneiden würde das ganze Array kopieren.
        var totalMs = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
        gc.Poll();

        return new DecodeStats(
            storage,
            source.Detected,
            channels,
            rate,
            count / channels,
            estimate,
            openMs,
            firstMs < 0 ? totalMs : firstMs,
            totalMs,
            resizes,
            (double)count * bytesPerSample / 1e6,
            (GC.GetTotalAllocatedBytes(precise: true) - allocatedBefore) / 1e6,
            gc.Count(2),
            gc.FullBlockingSeen,
            Math.Max(gc.MaxEphemeralPause, Math.Max(gc.MaxBackgroundPause, gc.MaxFullBlockingPause)));
    }

    private static int ToCapacity(long samples) => (int)Math.Min(samples, Array.MaxLength);

    private delegate int ReadSamples(Span<float> samples);

    private sealed record Source(ReadSamples Read, int Channels, int SampleRate, int Length, string Detected, IDisposable Owner);
}
