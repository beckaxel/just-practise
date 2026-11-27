using JustPractise.Core.Waveform;

namespace JustPractise.Audio.Player;

/// <summary>Berechnet die Spitzen der Wellenform aus einer fertig decodierten Datei.</summary>
/// <remarks>
/// Blockiert und gehört auf einen Hintergrund-Thread. Die Blöcke dürfen währenddessen nicht freigegeben werden;
/// der Player rechnet deshalb im selben Task wie das Decodieren, auf den die Freigabe wartet.
/// </remarks>
public static class WaveformReader
{
    private const int ChunkFrames = WaveformPeaks.BaseFrames * 1024;

    public static WaveformPeaks Read(DecodedAudio audio, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(audio);
        var frames = audio.Frames;
        var builder = new WaveformPeaksBuilder(audio.Channels, audio.SampleRate, frames);
        var chunk = new float[ChunkFrames * audio.Channels];
        for (long frame = 0; frame < frames; frame += ChunkFrames)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var read = audio.Read(frame, chunk);
            builder.Append(chunk.AsSpan(0, read));
        }

        return builder.Build();
    }
}
