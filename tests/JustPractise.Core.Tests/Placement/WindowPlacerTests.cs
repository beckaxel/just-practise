using JustPractise.Core.DataFiles;
using JustPractise.Core.Placement;
using JustPractise.Core.Settings;

namespace JustPractise.Core.Tests.Placement;

public sealed class WindowPlacerTests
{
    private static readonly ScreenArea FullHd = new(new PixelBox(0, 0, 1920, 1040), 1);
    private static readonly WindowPlacer Placer = new(defaultWidth: 1000, defaultHeight: 700);

    [Fact]
    public void CentersTheDefaultSizeOnTheFirstStart()
    {
        var placement = Placer.Place(null, [FullHd], FullHd)!;

        Assert.Equal(1000, placement.Width);
        Assert.Equal(700, placement.Height);
        Assert.Equal((1920 - 1016) / 2, placement.X);
        Assert.Equal((1040 - 748) / 2, placement.Y);
        Assert.False(placement.Maximized);
    }

    [Fact]
    public void ShrinksTheFirstWindowToASmallScaledScreen()
    {
        // 1366 × 768 mit 125 %, Taskleiste 48 px: nur 614 Einheiten hoch.
        var laptop = new ScreenArea(new PixelBox(0, 0, 1366, 720), 1.25);

        var placement = Placer.Place(null, [laptop], laptop)!;

        Assert.True(laptop.WorkingArea.Contains(Outer(placement, 1.25)));
        Assert.Equal(1000, placement.Width);
        Assert.Equal(576 - WindowPlacer.Frame.Height, placement.Height);
    }

    [Fact]
    public void CentersOnThePrimaryScreen()
    {
        var left = new ScreenArea(new PixelBox(-1920, 0, 1920, 1040), 1);

        var placement = Placer.Place(null, [left, FullHd], FullHd)!;

        Assert.True(FullHd.WorkingArea.Contains(Outer(placement, 1)));
    }

    [Fact]
    public void RestoresAVisibleWindowUnchanged()
    {
        var saved = new WindowPlacement { X = 100, Y = 50, Width = 1200, Height = 800, Maximized = true };

        Assert.Same(saved, Placer.Place(saved, [FullHd], FullHd));
    }

    [Fact]
    public void RestoresAWindowOnASecondScreen()
    {
        var left = new ScreenArea(new PixelBox(-2560, 0, 2560, 1400), 1);
        var saved = new WindowPlacement { X = -2000, Y = 100, Width = 1600, Height = 900 };

        Assert.Same(saved, Placer.Place(saved, [FullHd, left], FullHd));
    }

    [Fact]
    public void CentersAWindowWhoseScreenIsGone()
    {
        var saved = new WindowPlacement { X = -2000, Y = 100, Width = 1200, Height = 800, Maximized = true };

        var placement = Placer.Place(saved, [FullHd], FullHd)!;

        Assert.True(FullHd.WorkingArea.Contains(Outer(placement, 1)));
        Assert.Equal(1200, placement.Width);
        Assert.Equal(800, placement.Height);
        Assert.True(placement.Maximized);
    }

    [Fact]
    public void PushesAPartlyVisibleWindowBackOntoItsScreen()
    {
        var saved = new WindowPlacement { X = 1500, Y = 600, Width = 1000, Height = 700 };

        var placement = Placer.Place(saved, [FullHd], FullHd)!;

        Assert.True(FullHd.WorkingArea.Contains(Outer(placement, 1)));
        Assert.Equal(1920 - 1016, placement.X);
        Assert.Equal(1040 - 748, placement.Y);
        Assert.Equal(1000, placement.Width);
    }

    [Fact]
    public void ShrinksAWindowThatNoLongerFits()
    {
        var small = new ScreenArea(new PixelBox(0, 0, 1280, 680), 1);
        var saved = new WindowPlacement { X = 0, Y = 0, Width = 1800, Height = 1000 };

        var placement = Placer.Place(saved, [small], small)!;

        Assert.True(small.WorkingArea.Contains(Outer(placement, 1)));
        Assert.Equal(1280 - WindowPlacer.Frame.Width, placement.Width);
        Assert.Equal(680 - WindowPlacer.Frame.Height, placement.Height);
    }

    [Fact]
    public void LeavesThePlacementToTheSystemWithoutScreens()
    {
        Assert.Null(Placer.Place(null, [], null));
    }

    [Fact]
    public void WritesThePlacementToDeviceMemory()
    {
        var memory = new DeviceMemory { Window = new WindowPlacement { X = -20, Y = 10, Width = 1000, Height = 700, Maximized = true } };

        var json = AppDataFiles.Device.Write(memory);

        Assert.Contains("\"window\": {\n    \"x\": -20,\n    \"y\": 10,\n    \"width\": 1000,\n    \"height\": 700,\n    \"maximized\": true\n  }", json, StringComparison.Ordinal);
        Assert.Equal(memory.Window, AppDataFiles.Device.Read(json).Value.Window! with { Unknown = null });
    }

    [Fact]
    public void KeepsFractionalSizes()
    {
        var memory = new DeviceMemory { Window = new WindowPlacement { X = 10, Y = 10, Width = 998.4, Height = 700.8 } };

        var json = AppDataFiles.Device.Write(memory);
        var read = AppDataFiles.Device.Read(json).Value.Window!;

        Assert.Contains("\"width\": 998.4,", json, StringComparison.Ordinal);
        Assert.Equal(998.4, read.Width);
        Assert.Same(read, Placer.Place(read, [FullHd], FullHd));
    }

    private static PixelBox Outer(WindowPlacement placement, double scaling) => new(
        placement.X,
        placement.Y,
        (int)Math.Ceiling((placement.Width + WindowPlacer.Frame.Width) * scaling),
        (int)Math.Ceiling((placement.Height + WindowPlacer.Frame.Height) * scaling));
}
