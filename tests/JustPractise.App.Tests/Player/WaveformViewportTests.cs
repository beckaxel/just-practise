using JustPractise.App.Player;

namespace JustPractise.App.Tests.Player;

public sealed class WaveformViewportTests
{
    [Fact]
    public void StartsWithTheWholePiece()
    {
        var viewport = WaveformViewport.Whole(180);

        Assert.Equal((0, 180), (viewport.Start, viewport.Length));
        Assert.True(viewport.IsWhole);
        Assert.True(viewport.CanZoomIn);
    }

    [Fact]
    public void KeepsTheAnchorInPlaceWhenZooming()
    {
        var viewport = WaveformViewport.Whole(100).ZoomAround(4, 40);

        Assert.Equal(25, viewport.Length);
        Assert.Equal(30, viewport.Start); // 40 lag bei 40 % der Breite und liegt dort noch
    }

    [Fact]
    public void StaysInsideThePiece()
    {
        var viewport = WaveformViewport.Whole(100).ZoomAround(10, 99);

        Assert.Equal(10, viewport.Length);
        Assert.Equal(89.1, viewport.Start, 6); // 99 bleibt bei 99 % der Breite
        Assert.Equal(90, viewport.ZoomAround(1, 99).WithStart(95).Start);
        Assert.Equal(0, viewport.WithStart(-20).Start);
        Assert.Equal(90, viewport.WithStart(500).Start);
    }

    [Fact]
    public void ZoomsAtMostToHalfASecondAndAtLeastToTheWholePiece()
    {
        var viewport = WaveformViewport.Whole(100).ZoomAround(10_000, 50);

        Assert.Equal(WaveformViewport.MinLength, viewport.Length);
        Assert.False(viewport.CanZoomIn);
        Assert.True(viewport.ZoomAround(0.0001, 50).IsWhole);
        Assert.Equal(0.2, WaveformViewport.Whole(0.2).ZoomAround(4, 0.1).Length, 6); // ein sehr kurzes Stück bleibt ganz
    }

    [Fact]
    public void ZoomsAroundAVisiblePositionOrBringsItIntoTheMiddle()
    {
        var viewport = WaveformViewport.Of(100, 0, 20);

        Assert.Equal(WaveformViewport.Of(100, 5, 10), viewport.ZoomAtPosition(2, 10));
        Assert.Equal(WaveformViewport.Of(100, 55, 10), viewport.ZoomAtPosition(2, 60));
    }

    [Fact]
    public void TurnsAPageWhenThePositionRunsOutOnTheRight()
    {
        var viewport = WaveformViewport.Of(100, 0, 20);

        Assert.Equal(viewport, viewport.PageTo(15));
        Assert.Equal(WaveformViewport.Of(100, 20, 20), viewport.PageTo(20.1));
        Assert.Equal(WaveformViewport.Of(100, 48, 20), viewport.PageTo(50)); // weiter weg: kurz hinter dem linken Rand
        Assert.Equal(WaveformViewport.Of(100, 0, 20), WaveformViewport.Of(100, 50, 20).PageTo(1));
    }

    [Fact]
    public void FollowsAGrowingPieceOnlyWhenShowingAllOfIt()
    {
        Assert.Equal(WaveformViewport.Whole(60), WaveformViewport.Whole(10).WithDuration(60));
        Assert.Equal(WaveformViewport.Of(60, 2, 5), WaveformViewport.Of(10, 2, 5).WithDuration(60));
    }
}
