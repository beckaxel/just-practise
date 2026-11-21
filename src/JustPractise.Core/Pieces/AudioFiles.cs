namespace JustPractise.Core.Pieces;

/// <summary>Die Audioformate, die der Player abspielt. Nur sie werden vorerst zu Stücken gruppiert.</summary>
public static class AudioFiles
{
    private static readonly HashSet<string> Extensions = new(StringComparer.OrdinalIgnoreCase) { ".mp3", ".wav", ".flac", ".ogg" };

    public static bool IsSupported(string fileName) => Extensions.Contains(Path.GetExtension(fileName));
}
