using JustPractise.Core.Pieces;

namespace JustPractise.Core.Tests.Pieces;

public sealed class AudioFilesTests
{
    [Theory]
    [InlineData("Blues.mp3", true)]
    [InlineData("Blues.MP3", true)]
    [InlineData("Blues.wav", true)]
    [InlineData("Blues.flac", true)]
    [InlineData("Blues.ogg", true)]
    [InlineData("Blues.m4a", true)]
    [InlineData("Blues.M4A", true)]
    [InlineData("Blues.aac", true)]
    [InlineData("Blues.mp4", false)]
    [InlineData("Blues.pdf", false)]
    [InlineData("Blues.jps", false)]
    [InlineData("Eight days a week Playback mp3.mp3.asd", false)]
    [InlineData("mp3", false)]
    public void RecognizesThePlayableFormats(string fileName, bool supported) =>
        Assert.Equal(supported, AudioFiles.IsSupported(fileName));
}
