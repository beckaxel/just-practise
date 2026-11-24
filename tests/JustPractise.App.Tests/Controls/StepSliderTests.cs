using JustPractise.App.Controls;

namespace JustPractise.App.Tests.Controls;

public sealed class StepSliderTests
{
    [Theory]
    [InlineData(100, 1, 101)]
    [InlineData(100, -1, 99)]
    [InlineData(150, 1, 150)] // an der Grenze bleibt es stehen
    [InlineData(50, -1, 50)]
    [InlineData(99.7, 1, 101)] // erst ins Raster, dann einen Schritt
    public void StepsTheTempoInWholePercentWithinItsLimits(double value, int steps, double expected) =>
        Assert.Equal(expected, StepSlider.StepFrom(value, steps, step: 1, minimum: 50, maximum: 150));

    [Fact]
    public void StepsVolumeInHalfDecibels()
    {
        Assert.Equal(-2.5, StepSlider.StepFrom(-3, 1, 0.5, -20, 10));
        Assert.Equal(-20, StepSlider.StepFrom(-20, -1, 0.5, -20, 10));
        Assert.Equal(10, StepSlider.StepFrom(9.5, 1, 0.5, -20, 10));
    }
}
