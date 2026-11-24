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
    public void ReplacesAStaleSocketFromACrash()
    {
        File.WriteAllText(_directory.File("instance.sock"), "");

        using var first = SingleInstance.Start(_directory.Path, TimeSpan.Zero);

        Assert.NotNull(first);
    }
}
