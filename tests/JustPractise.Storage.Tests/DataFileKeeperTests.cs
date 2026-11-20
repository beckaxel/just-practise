using JustPractise.Core.DataFiles;
using JustPractise.Core.Settings;

namespace JustPractise.Storage.Tests;

public sealed class DataFileKeeperTests : IDisposable
{
    private readonly TemporaryDirectory _directory = new();

    public void Dispose() => _directory.Dispose();

    private DataFileStore<DeviceMemory> Store() => new(_directory.File("device.json"), AppDataFiles.Device);

    [Fact]
    public async Task KeepsTheValueAndWritesEveryChangeInOrder()
    {
        var keeper = new DataFileKeeper<DeviceMemory>(Store());

        for (var i = 0; i < 20; i++)
        {
            var folder = $"/music/{i}";
            keeper.Update(memory => memory with { LastFolder = folder });
        }

        Assert.Equal("/music/19", keeper.Value.LastFolder);
        await keeper.FlushAsync();
        Assert.Equal("/music/19", Store().Load().Value.LastFolder);
    }

    [Fact]
    public async Task WritesNothingWithoutAChange()
    {
        var keeper = new DataFileKeeper<DeviceMemory>(Store());

        keeper.Update(memory => memory);
        await keeper.FlushAsync();

        Assert.False(File.Exists(Store().Path));
    }

    [Fact]
    public async Task LeavesAFileFromANewerVersionAlone()
    {
        var store = Store();
        File.WriteAllText(store.Path, """{ "format": 99, "lastFolder": "/old" }""");
        var keeper = new DataFileKeeper<DeviceMemory>(store);

        keeper.Update(memory => memory with { LastFolder = "/new" });
        await keeper.FlushAsync();

        Assert.Equal("/new", keeper.Value.LastFolder);
        Assert.Contains("/old", File.ReadAllText(store.Path), StringComparison.Ordinal);
    }
}
