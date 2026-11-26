namespace JustPractise.Audio.Tuner;

/// <summary>
/// Ein Ringpuffer für Mono-Samples vom Eingabegerät: Der Audio-Thread schreibt (ohne Allokation, ohne Lock), ein
/// anderer Thread kopiert die jüngsten Samples zum Messen heraus.
/// </summary>
/// <remarks>
/// Ein Schreiber, ein Leser. Überholt der Schreiber den Leser während des Kopierens, enthält die Kopie für eine Messung
/// Samples aus zwei Durchläufen; bei einem Puffer von mehreren Fenstern ist das selten und für ein Stimmgerät harmlos.
/// </remarks>
public sealed class InputRing(int capacity)
{
    private readonly float[] _samples = new float[capacity];
    private long _written;

    /// <summary>Wie viele Samples bisher insgesamt geschrieben wurden.</summary>
    public long Written => Volatile.Read(ref _written);

    /// <summary>Vom Audio-Thread: hängt Samples an. Bei mehreren Kanälen wird nur der erste genommen.</summary>
    public void Write(ReadOnlySpan<float> interleaved, int channels)
    {
        var position = _written;
        for (var i = 0; i < interleaved.Length; i += channels)
        {
            _samples[position++ % _samples.Length] = interleaved[i];
        }

        Volatile.Write(ref _written, position);
    }

    /// <summary>Kopiert die jüngsten Samples nach <paramref name="destination"/>; gibt false zurück, solange zu wenige da sind.</summary>
    public bool CopyLatest(Span<float> destination)
    {
        var end = Written;
        if (end < destination.Length || destination.Length > _samples.Length)
        {
            return false;
        }

        var start = end - destination.Length;
        for (var i = 0; i < destination.Length; i++)
        {
            destination[i] = _samples[(start + i) % _samples.Length];
        }

        return true;
    }
}
