using JustPractise.Audio.Metronome;
using JustPractise.Audio.Player;
using JustPractise.Core.Metronome;

namespace JustPractise.Audio.Graph;

/// <summary>
/// Der Audio-Callback: wendet die Befehle an, mischt Player und Metronom und meldet den Status.
/// </summary>
/// <remarks>
/// Läuft auf dem Audio-Thread: keine Allokation, keine Sperre, kein I/O. Unabhängig von der Audio-Engine,
/// damit sich alles ohne Gerät testen lässt und der Callback bei Bedarf in nativen Code wandern kann.
/// Ersetzte Dateien gehen über <paramref name="retired"/> zurück und werden außerhalb freigegeben.
/// </remarks>
public sealed class AudioGraph(
    int channels,
    PlayerVoice player,
    ClickVoice click,
    CommandRing<AudioCommand> commands,
    CommandRing<RetiredTrack> retired,
    AudioStatus status)
{
    private long _frame;

    public int Channels { get; } = channels;

    /// <summary>Berechnet einen Block. <paramref name="buffer"/> ist interleaved mit <see cref="Channels"/> Kanälen.</summary>
    public void Render(Span<float> buffer)
    {
        while (commands.TryDequeue(out var command))
        {
            Apply(command);
        }

        buffer.Clear();
        player.Render(buffer, Channels);
        status.PublishPlayer(player.TrackId, player.Position, player.IsPlaying);

        if (click.Render(buffer, Channels, _frame) is { } beat)
        {
            status.PublishBeat(beat.Frame, beat.IndexInBar);
        }

        _frame += buffer.Length / Channels;
        status.PublishFrames(_frame);
    }

    private void Apply(in AudioCommand command)
    {
        switch (command.Kind)
        {
            case AudioCommandKind.MetronomeStart:
                click.Clock.Start(_frame);
                status.PublishMetronome(true);
                break;
            case AudioCommandKind.MetronomeStop:
                click.Stop();
                status.PublishMetronome(false);
                break;
            case AudioCommandKind.MetronomeTempo:
                click.Clock.SetTempo((int)command.Number, _frame);
                break;
            case AudioCommandKind.MetronomeMeter:
                click.Clock.SetMeter(new Meter((int)command.Number, command.Second));
                break;
            case AudioCommandKind.MetronomeVolume:
                click.SetVolume(command.Value);
                break;
            case AudioCommandKind.PlayerLoad:
                player.Load(command.Payload as LoadedTrack, out var previous);
                if (previous is not null)
                {
                    // Ist die Rückgabe voll, räumt der Finalizer des Stretchers auf; die Blöcke holt sich der GC.
                    retired.TryEnqueue(new RetiredTrack(previous));
                }

                break;
            case AudioCommandKind.PlayerPlay:
                player.Play();
                break;
            case AudioCommandKind.PlayerPause:
                player.Pause();
                break;
            case AudioCommandKind.PlayerSeek:
                player.Seek(command.Number);
                break;
            case AudioCommandKind.PlayerTempo:
                player.SetTempo(command.Value);
                break;
            case AudioCommandKind.PlayerVolume:
                player.SetVolume(command.Value);
                break;
            case AudioCommandKind.None:
            default:
                break;
        }
    }
}
