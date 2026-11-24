using Avalonia.Controls;
using JustPractise.App.Shell.ScreenSaver;

namespace JustPractise.App.Tests.Shell;

public sealed class KeepScreenAwakeTests
{
    [Fact]
    public void InhibitsWhileMaximizedOrFullScreen()
    {
        var inhibitor = new RecordingInhibitor();
        var keeper = new KeepScreenAwake(inhibitor);

        keeper.Update(WindowState.Normal);
        keeper.Update(WindowState.Maximized);
        keeper.Update(WindowState.FullScreen);
        keeper.Update(WindowState.Minimized);
        keeper.Update(WindowState.Normal);

        Assert.Equal([true, false], inhibitor.Calls);
    }

    [Fact]
    public void ReleasesWhenTheWindowLeavesMaximized()
    {
        var inhibitor = new RecordingInhibitor();
        var keeper = new KeepScreenAwake(inhibitor);

        keeper.Update(WindowState.Maximized);
        keeper.Update(WindowState.Normal);
        keeper.Update(WindowState.Maximized);

        Assert.Equal([true, false, true], inhibitor.Calls);
    }

    private sealed class RecordingInhibitor : IScreenSaverInhibitor
    {
        public List<bool> Calls { get; } = [];

        public void Inhibit(bool inhibit) => Calls.Add(inhibit);
    }
}
