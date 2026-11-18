using JustPractise.Audio.Graph;
using JustPractise.Audio.Metronome;
using JustPractise.Audio.Player;
using JustPractise.Core.Metronome;

namespace JustPractise.Audio.Tests.Graph;

// Braucht die native Bibliothek jpstretch; ohne sie werden die Tests übersprungen.
public sealed class AudioGraphPlayerTests
{
    private const int SampleRate = 48000;

    private readonly CommandRing<AudioCommand> _commands = new(64);
    private readonly CommandRing<RetiredTrack> _retired = new(4);
    private readonly AudioStatus _status = new();
    private readonly AudioGraph _graph;

    public AudioGraphPlayerTests()
    {
        Assert.SkipUnless(StretchVoice.IsAvailable(out var reason), reason);
        var click = new ClickVoice(new BeatClock(SampleRate, 120, Meter.FourFour), ClickSound.Create(SampleRate));
        _graph = new AudioGraph(2, new PlayerVoice(), click, _commands, _retired, _status);
    }

    private static LoadedTrack Track(int id)
    {
        var audio = new DecodedAudio(new BlockPool(blockSamples: 1 << 16), channels: 1, SampleRate);
        var tail = audio.WritableTail(SampleRate);
        tail.Fill(0.25f);
        audio.Commit(tail.Length);
        audio.Complete();
        return new LoadedTrack(id, audio, new StretchVoice(1, SampleRate));
    }

    private void Render(int blocks)
    {
        var block = new float[960];
        for (var i = 0; i < blocks; i++)
        {
            _graph.Render(block);
        }
    }

    [Fact]
    public void ReportsThePlayerPosition()
    {
        using var track = Track(7);
        _commands.TryEnqueue(AudioCommand.PlayerLoad(track));
        _commands.TryEnqueue(AudioCommand.PlayerPlay());

        Render(10);

        Assert.Equal(new PlayerInfo(7, 4800, true), _status.Player);
    }

    [Fact]
    public void HandsReplacedTracksBack()
    {
        using var first = Track(1);
        using var second = Track(2);
        _commands.TryEnqueue(AudioCommand.PlayerLoad(first));
        Render(1);
        Assert.False(_retired.TryDequeue(out _));

        _commands.TryEnqueue(AudioCommand.PlayerLoad(second));
        Render(1);

        Assert.True(_retired.TryDequeue(out var retired));
        Assert.Same(first, retired.Track);
        Assert.Equal(new PlayerInfo(2, 0, false), _status.Player);
    }

    [Fact]
    public void MixesPlayerAndMetronome()
    {
        using var track = Track(1);
        _commands.TryEnqueue(AudioCommand.PlayerLoad(track));
        _commands.TryEnqueue(AudioCommand.PlayerVolume(-120));
        _commands.TryEnqueue(AudioCommand.PlayerPlay());
        _commands.TryEnqueue(AudioCommand.MetronomeStart());
        var block = new float[960];

        _graph.Render(block);

        Assert.True(_status.MetronomeRunning);
        Assert.True(_status.Player.IsPlaying);
        Assert.Contains(block, sample => Math.Abs(sample) > 0.1f);
    }
}
