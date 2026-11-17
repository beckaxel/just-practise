namespace JustPractise.Prototypes.DecodeCheck;

/// <summary>
/// Hält Blöcke fester Größe für decodiertes Audio. Beim Wechsel der Datei kommen die Blöcke zurück
/// und werden für die nächste Datei wiederverwendet, statt dass jedes Mal ein neues großes Array entsteht.
/// Nur vom Lade-Thread benutzt, nicht vom Audio-Thread.
/// </summary>
internal sealed class BlockPool
{
    /// <summary>1 Mi Samples = 4 MB pro Block, 15 min Stereo bei 48 kHz sind rund 83 Blöcke.</summary>
    public const int BlockSamples = 1 << 20;

    private readonly Stack<float[]> _free = new();

    public int Created { get; private set; }

    public int Free => _free.Count;

    public float[] Rent()
    {
        if (_free.TryPop(out var block))
        {
            return block;
        }

        Created++;
        return GC.AllocateUninitializedArray<float>(BlockSamples);
    }

    public void Return(float[] block) => _free.Push(block);
}

/// <summary>
/// Decodiertes Audio, interleaved in Blöcken aus einem <see cref="BlockPool"/>. Ein Block enthält nur ganze Frames,
/// so liegt ein Frame nie über einer Blockgrenze. Die Position eines Frames ergibt sich durch eine Division.
/// </summary>
internal sealed class BlockAudio(BlockPool pool, int channels)
{
    private float[][] _blocks = new float[16][];
    private int _blockCount;

    public int Channels { get; } = channels;

    public int FramesPerBlock { get; } = BlockPool.BlockSamples / channels;

    public long Frames { get; private set; }

    public int Blocks => _blockCount;

    /// <summary>Freier Platz am Ende, in den der Decoder direkt schreibt. Holt bei Bedarf einen neuen Block.</summary>
    public Span<float> WritableTail(int maxSamples)
    {
        var used = (int)(Frames % FramesPerBlock);
        if (used == 0 && Frames == (long)_blockCount * FramesPerBlock)
        {
            if (_blockCount == _blocks.Length)
            {
                Array.Resize(ref _blocks, _blocks.Length * 2);
            }

            _blocks[_blockCount++] = pool.Rent();
        }

        var block = _blocks[_blockCount - 1];
        var free = (FramesPerBlock - used) * Channels;
        var samples = Math.Min(free, maxSamples) / Channels * Channels;
        return block.AsSpan(used * Channels, samples);
    }

    /// <summary>Meldet, wie viele Samples in den zuletzt geholten Platz geschrieben wurden.</summary>
    public void Commit(int samples) => Frames += samples / Channels;

    /// <summary>Kopiert Frames ab <paramref name="frame"/>, wie es der Audio-Callback tun würde. Ohne Allokation.</summary>
    public int Read(long frame, Span<float> destination)
    {
        var written = 0;
        while (written < destination.Length && frame < Frames)
        {
            var block = (int)(frame / FramesPerBlock);
            var offset = (int)(frame % FramesPerBlock);
            var frames = (int)Math.Min(Math.Min(FramesPerBlock - offset, Frames - frame), (destination.Length - written) / Channels);
            if (frames == 0)
            {
                break;
            }

            _blocks[block].AsSpan(offset * Channels, frames * Channels).CopyTo(destination[written..]);
            written += frames * Channels;
            frame += frames;
        }

        return written;
    }

    /// <summary>Gibt alle Blöcke an den Pool zurück, z. B. beim Wechsel der Datei.</summary>
    public void Release()
    {
        for (var i = 0; i < _blockCount; i++)
        {
            pool.Return(_blocks[i]);
            _blocks[i] = null!;
        }

        _blockCount = 0;
        Frames = 0;
    }
}
