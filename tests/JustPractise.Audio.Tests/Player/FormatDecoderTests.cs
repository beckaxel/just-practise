using JustPractise.Audio.Engine;
using JustPractise.Audio.Player;

namespace JustPractise.Audio.Tests.Player;

public sealed class FormatDecoderTests
{
    private readonly RecordingDecoder _engine = new();
    private readonly RecordingDecoder _aac = new();

    [Theory]
    [InlineData("Blues.m4a", true)]
    [InlineData("Blues.M4A", true)]
    [InlineData("Blues.aac", true)]
    [InlineData("Blues.mp3", false)]
    [InlineData("Blues.wav", false)]
    [InlineData("Blues.flac", false)]
    [InlineData("Blues.ogg", false)]
    public void ChoosesTheDecoderByExtension(string fileName, bool aac)
    {
        new FormatDecoder(_engine, _aac).Decode(fileName, new BlockPool(), _ => { }, TestContext.Current.CancellationToken);

        Assert.Equal(aac ? [fileName] : [], _aac.Paths);
        Assert.Equal(aac ? [] : [fileName], _engine.Paths);
    }

    private sealed class RecordingDecoder : IAudioDecoder
    {
        public List<string> Paths { get; } = [];

        public void Decode(string path, BlockPool pool, Action<DecodedAudio> opened, CancellationToken cancellationToken) =>
            Paths.Add(path);
    }
}
