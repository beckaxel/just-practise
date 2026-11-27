using JustPractise.Audio.Engine;

namespace JustPractise.Audio.Player;

/// <summary>Wählt den Decoder nach der Endung: M4A und AAC über FFmpeg, alles andere über die Audio-Engine.</summary>
public sealed class FormatDecoder(IAudioDecoder engineDecoder, IAudioDecoder aacDecoder) : IAudioDecoder
{
    private static readonly HashSet<string> AacExtensions = new(StringComparer.OrdinalIgnoreCase) { ".m4a", ".aac" };

    /// <inheritdoc cref="FileDecoder.Decode"/>
    public void Decode(string path, BlockPool pool, Action<DecodedAudio> opened, CancellationToken cancellationToken) =>
        (AacExtensions.Contains(Path.GetExtension(path)) ? aacDecoder : engineDecoder).Decode(path, pool, opened, cancellationToken);
}
