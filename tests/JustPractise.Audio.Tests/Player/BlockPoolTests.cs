using JustPractise.Audio.Player;

namespace JustPractise.Audio.Tests.Player;

public sealed class BlockPoolTests
{
    [Fact]
    public void ReusesReturnedBlocks()
    {
        var pool = new BlockPool(blockSamples: 16);
        var block = pool.Rent();

        pool.Return(block);

        Assert.Same(block, pool.Rent());
        Assert.Equal(16, block.Length);
    }

    [Fact]
    public void KeepsOnlyAsManyFreeBlocksAsAllowed()
    {
        var pool = new BlockPool(blockSamples: 16);
        var blocks = Enumerable.Range(0, 10).Select(_ => pool.Rent()).ToList();
        blocks.ForEach(pool.Return);

        pool.Trim(3);

        Assert.Equal(3, pool.FreeBlocks);
    }

    [Fact]
    public void RejectsForeignBlocks() =>
        Assert.Throws<ArgumentException>(() => new BlockPool(blockSamples: 16).Return(new float[8]));

    [Fact]
    public void UsesFourMegabyteBlocksByDefault() =>
        Assert.Equal(4 * 1024 * 1024, new BlockPool().BlockSamples * sizeof(float));
}
