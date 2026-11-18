using JustPractise.Audio.Metronome;
using JustPractise.Core.Metronome;

namespace JustPractise.Audio.Graph;

/// <summary>
/// Der Audio-Callback: wendet die Befehle an, mischt Player und Metronom und meldet den Status.
/// </summary>
/// <remarks>
/// Läuft auf dem Audio-Thread: keine Allokation, keine Sperre, kein I/O. Unabhängig von der Audio-Engine,
/// damit sich alles ohne Gerät testen lässt und der Callback bei Bedarf in nativen Code wandern kann.
/// </remarks>
public sealed class AudioGraph(int channels, ClickVoice click, CommandRing<AudioCommand> commands, AudioStatus status)
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
                click.Clock.SetTempo(command.Number, _frame);
                break;
            case AudioCommandKind.MetronomeMeter:
                click.Clock.SetMeter(new Meter(command.Number, command.Second));
                break;
            case AudioCommandKind.MetronomeVolume:
                click.SetVolume(command.Value);
                break;
            case AudioCommandKind.None:
            default:
                break;
        }
    }
}
