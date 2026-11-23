namespace JustPractise.Audio.Player;

/// <summary>
/// Hält Blöcke fester Größe für decodiertes Audio. Beim Wechsel der Datei kommen die Blöcke zurück und
/// werden wiederverwendet (Prüfpunkt 7). Die Blöcke liegen im nativen Speicher (siehe <see cref="AudioBlock"/>).
/// </summary>
/// <remarks>Threadsicher, aber nie vom Audio-Thread aus benutzen.</remarks>
public sealed class BlockPool(int blockSamples = BlockPool.DefaultBlockSamples) : IDisposable
{
    /// <summary>1 Mi Samples = 4 MB pro Block. 15 Minuten Stereo bei 48 kHz sind rund 83 Blöcke.</summary>
    public const int DefaultBlockSamples = 1 << 20;

    private readonly Lock _lock = new();
    private readonly Stack<AudioBlock> _free = new();
    private bool _disposed;

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

    public AudioBlock Rent()
    {
        lock (_lock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_free.TryPop(out var block))
            {
                return block;
            }
        }

        return new AudioBlock(BlockSamples);
    }

    /// <summary>Nimmt einen Block zurück. Nach <see cref="Dispose"/> wird er gleich freigegeben.</summary>
    public void Return(AudioBlock block)
    {
        if (block.Length != BlockSamples)
        {
            throw new ArgumentException("The block does not belong to this pool.", nameof(block));
        }

        lock (_lock)
        {
            if (!_disposed)
            {
                _free.Push(block);
                return;
            }
        }

        block.Dispose();
    }

    /// <summary>
    /// Behält höchstens <paramref name="maxFreeBlocks"/> freie Blöcke und gibt den Rest frei.
    /// Die App ruft das nach einem Dateiwechsel mit der Blockzahl der aktuellen Datei auf.
    /// </summary>
    public void Trim(int maxFreeBlocks)
    {
        lock (_lock)
        {
            while (_free.Count > maxFreeBlocks)
            {
                _free.Pop().Dispose();
            }
        }
    }

    /// <summary>Gibt die freien Blöcke frei; Blöcke, die noch verliehen sind, werden bei der Rückgabe freigegeben.</summary>
    public void Dispose()
    {
        lock (_lock)
        {
            _disposed = true;
        }

        Trim(0);
    }
}
