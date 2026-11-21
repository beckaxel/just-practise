using SoundFlow.Backends.MiniAudio;
using SoundFlow.Providers;

namespace JustPractise.Prototypes.GcCheck;

/// <summary>Ein vollständig decodierter Ausschnitt einer Audiodatei, interleaved.</summary>
internal sealed class AudioClip(float[] samples, int channels, int sampleRate)
{
    public float[] Samples { get; } = samples;

    public int Channels { get; } = channels;

    public int SampleRate { get; } = sampleRate;

    public long Frames => Samples.Length / Channels;

    public double Seconds => (double)Frames / SampleRate;

    public static AudioClip Load(MiniAudioEngine engine, string path, double startSeconds, double? lengthSeconds)
    {
        using var stream = File.OpenRead(path);
        using var provider = new StreamDataProvider(engine, stream);
        var channels = provider.FormatInfo?.ChannelCount ?? 2;
        var skip = (long)(startSeconds * provider.SampleRate) * channels;
        var wanted = lengthSeconds is null ? long.MaxValue : (long)(lengthSeconds.Value * provider.SampleRate) * channels;

        var samples = new List<float>();
        var buffer = new float[16384];
        int read;
        while (samples.Count < wanted && (read = provider.ReadBytes(buffer)) > 0)
        {
            var span = buffer.AsSpan(0, read);
            if (skip > 0)
            {
                var skipped = (int)Math.Min(skip, read);
                skip -= skipped;
                span = span[skipped..];
            }

            var take = (int)Math.Min(span.Length, wanted - samples.Count);
            samples.AddRange(span[..take]);
        }

        return new AudioClip([.. samples], channels, provider.SampleRate);
    }

    /// <summary>
    /// Kopiert Frames ab <paramref name="startFrame"/>. Hinter dem Ende wird bei <paramref name="loop"/>
    /// von vorn weitergelesen, sonst mit Stille aufgefüllt. Vor dem Anfang steht immer Stille. Ohne Allokation.
    /// </summary>
    public void Read(long startFrame, Span<float> destination, bool loop)
    {
        var frames = Frames;
        var written = 0;
        while (written < destination.Length)
        {
            var frame = startFrame + (written / Channels);
            if (frame < 0)
            {
                var silence = (int)Math.Min(-frame * Channels, destination.Length - written);
                destination.Slice(written, silence).Clear();
                written += silence;
                continue;
            }

            if (loop)
            {
                frame %= frames;
            }
            else if (frame >= frames)
            {
                destination[written..].Clear();
                return;
            }

            var available = (int)Math.Min((frames - frame) * Channels, destination.Length - written);
            Samples.AsSpan((int)(frame * Channels), available).CopyTo(destination[written..]);
            written += available;
        }
    }
}
