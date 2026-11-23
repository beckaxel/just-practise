namespace JustPractise.Audio.Player;

/// <summary>
/// Eine ganz decodierte Datei als float, interleaved, in Blöcken aus einem <see cref="BlockPool"/>.
/// Ein Block enthält nur ganze Frames, so liegt ein Frame nie über einer Blockgrenze.
/// </summary>
/// <remarks>
/// Ein Lade-Thread schreibt, der Audio-Thread liest gleichzeitig: Er sieht immer nur die Frames,
/// die schon fertig sind (<see cref="Frames"/>). Die Wiedergabe kann also starten, bevor alles decodiert ist.
/// Die Blockliste hat eine feste Länge und wird nie vergrößert, damit der Leser ohne Sperre auskommt.
/// </remarks>
public sealed class DecodedAudio
{
    /// <summary>Höchstens so viele Blöcke, mit Standardblöcken 512 MB oder rund 23 Minuten Stereo bei 48 kHz.</summary>
    public const int MaxBlocks = 128;

    private readonly BlockPool _pool;
    private readonly AudioBlock?[] _blocks = new AudioBlock?[MaxBlocks];
    private int _blockCount;
    private long _frames;
    private int _state;

    public DecodedAudio(BlockPool pool, int channels, int sampleRate)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(channels);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sampleRate);
        _pool = pool;
        Channels = channels;
        SampleRate = sampleRate;
        FramesPerBlock = pool.BlockSamples / channels;
    }

    public int Channels { get; }

    public int SampleRate { get; }

    public int FramesPerBlock { get; }

    /// <summary>Die Frames, die schon decodiert sind und gelesen werden dürfen.</summary>
    public long Frames => Volatile.Read(ref _frames);

    /// <summary>Die Datei ist fertig decodiert (oder wurde wegen <see cref="MaxBlocks"/> abgeschnitten).</summary>
    public bool IsComplete => Volatile.Read(ref _state) >= (int)State.Complete;

    /// <summary>Die Datei war zu lang und wurde nach <see cref="MaxBlocks"/> Blöcken abgeschnitten.</summary>
    public bool IsTruncated => Volatile.Read(ref _state) == (int)State.Truncated;

    public int BlockCount => _blockCount;

    public TimeSpan Duration => TimeSpan.FromSeconds((double)Frames / SampleRate);

    /// <summary>
    /// Freier Platz am Ende, in den der Decoder direkt schreibt, höchstens <paramref name="maxSamples"/> und nur ganze Frames.
    /// Ist kein Platz mehr, ist der Bereich leer und die Datei als abgeschnitten markiert. Nur vom Lade-Thread.
    /// </summary>
    public Span<float> WritableTail(int maxSamples)
    {
        var frames = _frames;
        var used = (int)(frames % FramesPerBlock);
        if (used == 0 && frames == (long)_blockCount * FramesPerBlock)
        {
            if (_blockCount == MaxBlocks)
            {
                Volatile.Write(ref _state, (int)State.Truncated);
                return [];
            }

            _blocks[_blockCount++] = _pool.Rent();
        }

        var block = _blocks[_blockCount - 1]!;
        var free = (FramesPerBlock - used) * Channels;
        var samples = Math.Min(free, maxSamples) / Channels * Channels;
        return block.AsSpan(used * Channels, samples);
    }

    /// <summary>Gibt die zuletzt geschriebenen Samples zum Lesen frei. Nur vom Lade-Thread.</summary>
    public void Commit(int samples) => Volatile.Write(ref _frames, _frames + (samples / Channels));

    /// <summary>Meldet, dass die Datei fertig decodiert ist. Nur vom Lade-Thread.</summary>
    public void Complete() => Interlocked.CompareExchange(ref _state, (int)State.Complete, (int)State.Decoding);

    /// <summary>
    /// Kopiert Frames ab <paramref name="frame"/> nach <paramref name="destination"/> (interleaved, gleiche Kanalzahl).
    /// Liefert die Anzahl kopierter Samples; hinter den fertigen Frames wird nichts geschrieben.
    /// Läuft auf dem Audio-Thread: keine Allokation, keine Sperre.
    /// </summary>
    public int Read(long frame, Span<float> destination)
    {
        var available = Frames;
        var written = 0;
        while (written < destination.Length && frame >= 0 && frame < available)
        {
            var block = (int)(frame / FramesPerBlock);
            var offset = (int)(frame % FramesPerBlock);
            var frames = (int)Math.Min(Math.Min(FramesPerBlock - offset, available - frame), (destination.Length - written) / Channels);
            if (frames == 0)
            {
                break;
            }

            Volatile.Read(ref _blocks[block])!.AsSpan(offset * Channels, frames * Channels).CopyTo(destination[written..]);
            written += frames * Channels;
            frame += frames;
        }

        return written;
    }

    /// <summary>
    /// Gibt alle Blöcke an den Pool zurück. Erst aufrufen, wenn der Audio-Thread die Datei nicht mehr liest
    /// und der Decoder fertig oder abgebrochen ist.
    /// </summary>
    public void Release()
    {
        for (var i = 0; i < _blockCount; i++)
        {
            _pool.Return(_blocks[i]!);
            _blocks[i] = null;
        }

        _blockCount = 0;
        Volatile.Write(ref _frames, 0);
    }

    private enum State
    {
        Decoding,
        Complete,
        Truncated,
    }
}
