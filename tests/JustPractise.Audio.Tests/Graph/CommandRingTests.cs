using JustPractise.Audio.Graph;

namespace JustPractise.Audio.Tests.Graph;

public sealed class CommandRingTests
{
    [Fact]
    public void DeliversInOrder()
    {
        var ring = new CommandRing<int>(4);
        ring.TryEnqueue(1);
        ring.TryEnqueue(2);
        ring.TryEnqueue(3);

        Assert.True(ring.TryDequeue(out var first));
        Assert.True(ring.TryDequeue(out var second));
        Assert.True(ring.TryDequeue(out var third));
        Assert.False(ring.TryDequeue(out _));
        Assert.Equal([1, 2, 3], [first, second, third]);
    }

    [Fact]
    public void RefusesWhenFullAndAcceptsAgainAfterDequeuing()
    {
        var ring = new CommandRing<int>(2);

        Assert.True(ring.TryEnqueue(1));
        Assert.True(ring.TryEnqueue(2));
        Assert.False(ring.TryEnqueue(3));

        ring.TryDequeue(out _);
        Assert.True(ring.TryEnqueue(3));
    }

    [Fact]
    public void WrapsAroundManyTimes()
    {
        var ring = new CommandRing<int>(4);
        for (var i = 0; i < 1000; i++)
        {
            ring.TryEnqueue(i);
            Assert.True(ring.TryDequeue(out var item));
            Assert.Equal(i, item);
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(100)]
    public void RequiresAPowerOfTwo(int capacity) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new CommandRing<int>(capacity));

    [Fact]
    public async Task DeliversEverythingAcrossThreads()
    {
        const int Count = 200_000;
        var ring = new CommandRing<int>(64);
        var cancellation = TestContext.Current.CancellationToken;

        var producers = Enumerable.Range(0, 2).Select(producer => Task.Run(
            () =>
            {
                for (var i = 0; i < Count; i++)
                {
                    while (!ring.TryEnqueue((producer * Count) + i))
                    {
                        Thread.Yield();
                    }
                }
            },
            cancellation)).ToArray();

        var received = new List<int>(2 * Count);
        var consumer = Task.Run(
            () =>
            {
                while (received.Count < 2 * Count)
                {
                    if (ring.TryDequeue(out var item))
                    {
                        received.Add(item);
                    }
                }
            },
            cancellation);

        await Task.WhenAll([.. producers, consumer]);

        // Alles kommt an, und die Befehle eines Erzeugers bleiben in ihrer Reihenfolge.
        Assert.Equal(Enumerable.Range(0, Count), received.Where(item => item < Count));
        Assert.Equal(Enumerable.Range(Count, Count), received.Where(item => item >= Count));
    }

    [Fact]
    public void DequeuesWithoutAllocating()
    {
        var ring = new CommandRing<AudioCommand>(16);
        ring.TryEnqueue(AudioCommand.MetronomeStart());
        ring.TryDequeue(out _);

        ring.TryEnqueue(AudioCommand.MetronomeTempo(90));
        var before = GC.GetAllocatedBytesForCurrentThread();
        ring.TryDequeue(out var command);

        Assert.Equal(before, GC.GetAllocatedBytesForCurrentThread());
        Assert.Equal(90, command.Number);
    }
}
