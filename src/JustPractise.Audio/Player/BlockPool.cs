namespace JustPractise.Audio.Player;

/// <summary>
/// Hält Blöcke fester Größe für decodiertes Audio. Beim Wechsel der Datei kommen die Blöcke zurück und
/// werden wiederverwendet, statt dass jedes Mal ein großes Array entsteht (Prüfpunkt 7).
/// </summary>
/// <remarks>Threadsicher, aber nie vom Audio-Thread aus benutzen.</remarks>
public sealed class BlockPool(int blockSamples = BlockPool.DefaultBlockSamples)
{
    /// <summary>1 Mi Samples = 4 MB pro Block. 15 Minuten Stereo bei 48 kHz sind rund 83 Blöcke.</summary>
    public const int DefaultBlockSamples = 1 << 20;

    private readonly Lock _lock = new();
    private readonly Stack<float[]> _free = new();

    public int BlockSamples { get; } = blockSamples;

    public int FreeBlocks
    {
        get
        {
            lock (_lock)
            {
                return _free.Count;
            }
        }
    }

    public float[] Rent()
    {
        lock (_lock)
        {
            if (_free.TryPop(out var block))
            {
                return block;
            }
        }

        return GC.AllocateUninitializedArray<float>(BlockSamples);
    }

    public void Return(float[] block)
    {
        if (block.Length != BlockSamples)
        {
            throw new ArgumentException("The block does not belong to this pool.", nameof(block));
        }

        lock (_lock)
        {
            _free.Push(block);
        }
    }

    /// <summary>
    /// Behält höchstens <paramref name="maxFreeBlocks"/> freie Blöcke, den Rest räumt die Speicherbereinigung ab.
    /// Die App ruft das nach einem Dateiwechsel mit der Blockzahl der aktuellen Datei auf.
    /// </summary>
    public void Trim(int maxFreeBlocks)
    {
        lock (_lock)
        {
            while (_free.Count > maxFreeBlocks)
            {
                _free.Pop();
            }
        }
    }
}
