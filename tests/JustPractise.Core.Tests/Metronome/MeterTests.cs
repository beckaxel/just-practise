using JustPractise.Core.Metronome;

namespace JustPractise.Core.Tests.Metronome;

public sealed class MeterTests
{
    [Theory]
    [InlineData("3/4", 3, 4)]
    [InlineData("4/4", 4, 4)]
    [InlineData("7/8", 7, 8)]
    [InlineData("12/8", 12, 8)]
    public void ParsesValidMeters(string text, int beats, int noteValue)
    {
        Assert.True(Meter.TryParse(text, out var meter));
        Assert.Equal(new Meter(beats, noteValue), meter);
        Assert.Equal(text, meter.ToString());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("4")]
    [InlineData("4/3")]
    [InlineData("0/4")]
    [InlineData(" 4/4")]
    [InlineData("-3/4")]
    [InlineData("4/4/4")]
    public void RejectsInvalidMeters(string? text) => Assert.False(Meter.TryParse(text, out _));
}
