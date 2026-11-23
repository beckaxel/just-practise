using JustPractise.Audio.Player;

namespace JustPractise.Audio.Tests.Player;

public sealed class BlockPoolTests
{
    [Fact]
    public void ReusesReturnedBlocks()
    {
        using var pool = new BlockPool(blockSamples: 16);
        var block = pool.Rent();

        pool.Return(block);

        Assert.Same(block, pool.Rent());
        Assert.Equal(16, block.Length);
    }

    [Fact]
    public void KeepsOnlyAsManyFreeBlocksAsAllowed()
    {
        using var pool = new BlockPool(blockSamples: 16);
        var blocks = Enumerable.Range(0, 10).Select(_ => pool.Rent()).ToList();
        blocks.ForEach(pool.Return);

        pool.Trim(3);

        Assert.Equal(3, pool.FreeBlocks);
    }

    [Fact]
    public void RejectsForeignBlocks()
    {
        using var pool = new BlockPool(blockSamples: 16);
        using var foreign = new AudioBlock(8);

        Assert.Throws<ArgumentException>(() => pool.Return(foreign));
    }

    [Fact]
    public void FreesTheBlocksItDoesNotKeep()
    {
        using var pool = new BlockPool(blockSamples: 16);
        var blocks = Enumerable.Range(0, 3).Select(_ => pool.Rent()).ToList();
        blocks.ForEach(pool.Return);

        pool.Trim(1);

        Assert.Equal(2, blocks.Count(block => block.IsClosed));
    }

    [Fact]
    public void FreesBlocksThatComeBackAfterDisposal()
    {
        var pool = new BlockPool(blockSamples: 16);
        var kept = pool.Rent();
        var lent = pool.Rent();
        pool.Return(kept);

        pool.Dispose();
        pool.Return(lent);

        Assert.True(kept.IsClosed);
        Assert.True(lent.IsClosed);
        Assert.Throws<ObjectDisposedException>(() => pool.Rent());
    }

    [Fact]
    public void BlocksHoldTheirSamples()
    {
        using var block = new AudioBlock(16);

        block.AsSpan(0, 16).Fill(0.5f);
        block.AsSpan(4, 2).Fill(1f);

        Assert.Equal([0.5f, 1f, 1f, 0.5f], block.AsSpan(3, 4).ToArray());
        Assert.Throws<ArgumentOutOfRangeException>(() => block.AsSpan(10, 7));
    }

    [Fact]
    public void UsesFourMegabyteBlocksByDefault() =>
        Assert.Equal(4 * 1024 * 1024, new BlockPool().BlockSamples * sizeof(float));
}
