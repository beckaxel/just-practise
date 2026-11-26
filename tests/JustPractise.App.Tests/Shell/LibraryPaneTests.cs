using JustPractise.App.Shell;
using JustPractise.Core.DataFiles;
using JustPractise.Core.Settings;
using JustPractise.Storage;

namespace JustPractise.App.Tests.Shell;

public sealed class LibraryPaneTests : IAsyncDisposable
{
    private readonly TemporaryDirectory _directory = new();
    private readonly List<DataFileKeeper<DeviceMemory>> _memories = [];

    public async ValueTask DisposeAsync()
    {
        foreach (var memory in _memories)
        {
            await memory.FlushAsync();
        }

        _directory.Dispose();
    }

    private DataFileKeeper<DeviceMemory> Memory()
    {
        var memory = new DataFileKeeper<DeviceMemory>(new DataFileStore<DeviceMemory>(_directory.File("device.json"), AppDataFiles.Device));
        _memories.Add(memory);
        return memory;
    }

    [Fact]
    public void StartsExpandedWithTheDefaultWidthAndSavesNothing()
    {
        var memory = Memory();
        var pane = new LibraryPane(memory);

        Assert.False(pane.IsCollapsed);
        Assert.Equal(LibraryPane.DefaultWidth, pane.Width);
        Assert.True(memory.Value.Layout.IsDefault);
    }

    [Fact]
    public async Task CollapsesAndExpandsToTheLastWidthAndRemembersBoth()
    {
        var memory = Memory();
        var pane = new LibraryPane(memory);
        pane.Resized(520.4);
        pane.ToggleCommand.Execute(null);
        pane.Resized(56); // eingeklappt ändert der Trenner nichts
        await memory.FlushAsync();

        var next = new LibraryPane(Memory());

        Assert.True(next.IsCollapsed);
        Assert.Equal(520, next.Width);
        next.ToggleCommand.Execute(null);
        Assert.False(next.IsCollapsed);
        Assert.Equal(520, next.Width);
    }

    [Fact]
    public void KeepsTheWidthWithinItsLimits()
    {
        var pane = new LibraryPane(Memory());

        pane.Resized(10);
        Assert.Equal(LibraryPane.MinWidth, pane.Width);

        pane.Resized(5000);
        Assert.Equal(LibraryPane.MaxWidth, pane.Width);
    }
}
