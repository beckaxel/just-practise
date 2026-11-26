using JustPractise.App.Shell;

namespace JustPractise.App.Tests.Shell;

public sealed class SingleInstanceTests : IDisposable
{
    private readonly TemporaryDirectory _directory = new();

    public void Dispose() => _directory.Dispose();

    [Fact]
    public void TheFirstStartBecomesTheInstance()
    {
        using var first = SingleInstance.Start(_directory.Path, TimeSpan.Zero);

        Assert.NotNull(first);
    }

    [Fact]
    public async Task ASecondStartActivatesTheFirstAndEnds()
    {
        using var first = SingleInstance.Start(_directory.Path, TimeSpan.Zero)!;
        var activated = new TaskCompletionSource();
        first.ActivationRequested += (_, _) => activated.TrySetResult();

        var second = SingleInstance.Start(_directory.Path, TimeSpan.FromSeconds(2));

        Assert.Null(second);
        await activated.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
    }

    [Fact]
    public void AStartAfterTheFirstEndedBecomesTheInstance()
    {
        SingleInstance.Start(_directory.Path, TimeSpan.Zero)!.Dispose();

        using var next = SingleInstance.Start(_directory.Path, TimeSpan.Zero);

        Assert.NotNull(next);
    }

    [Fact]
    public void StartsWhenThePathIsTooLongForASocket()
    {
        var deep = Path.Combine(_directory.Path, new string('x', 120));

        using var first = SingleInstance.Start(deep, TimeSpan.Zero);
        using var second = SingleInstance.Start(deep, TimeSpan.FromMilliseconds(200));

        Assert.NotNull(first);
        Assert.NotNull(second); // ohne Socket startet eine weitere Instanz nach dem Timeout doch, statt gar nicht
    }

    [Fact]
    public void ReplacesAStaleSocketFromACrash()
    {
        File.WriteAllText(_directory.File("instance.sock"), "");

        using var first = SingleInstance.Start(_directory.Path, TimeSpan.Zero);

        Assert.NotNull(first);
    }
}
