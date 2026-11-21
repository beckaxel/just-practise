namespace JustPractise.Audio.Graph;

/// <summary>Ein Schlag, wie ihn die Oberfläche für die Schlaganzeige sieht.</summary>
/// <param name="Frame">das Sample, in dem der Schlag begann</param>
/// <param name="IndexInBar">der Schlag im Takt, 0 ist die Eins</param>
public readonly record struct BeatInfo(long Frame, int IndexInBar);

/// <summary>Der Stand des Players, wie ihn die Oberfläche sieht.</summary>
/// <param name="TrackId">die Kennung der Datei, zu der die Angaben gehören (die unteren 16 Bit), 0 ohne Datei</param>
/// <param name="Position">die gerade hörbare Stelle in Frames der Datei</param>
/// <param name="IsPlaying">spielt gerade; am Ende hält der Player von selbst an</param>
public readonly record struct PlayerInfo(int TrackId, long Position, bool IsPlaying);

/// <summary>
/// Was der Audio-Thread nach jedem Block meldet. Die Oberfläche liest es regelmäßig ab.
/// </summary>
/// <remarks>
/// Nur Zahlen in Feldern, die atomar geschrieben werden: keine Events, keine Allokation, keine Sperre.
/// Was zusammengehört (Schlag und sein Sample), steckt in einem einzigen <c>long</c>.
/// </remarks>
public sealed class AudioStatus
{
    private const int IndexBits = 8;
    private const long IndexMask = (1 << IndexBits) - 1;
    private const long NoBeat = -1;

    private const int TrackBits = 16;
    private const long TrackMask = (1 << TrackBits) - 1;

    private long _renderedFrames;
    private long _player;
    private long _lastBeat = NoBeat;
    private int _metronomeRunning;

    /// <summary>Wie viele Frames der Audio-Thread bisher berechnet hat.</summary>
    public long RenderedFrames => Volatile.Read(ref _renderedFrames);

    public bool MetronomeRunning => Volatile.Read(ref _metronomeRunning) != 0;

    /// <summary>Der zuletzt berechnete Schlag, <c>null</c> vor dem ersten Schlag oder nach dem Stopp.</summary>
    public BeatInfo? LastBeat
    {
        get
        {
            var packed = Volatile.Read(ref _lastBeat);
            return packed == NoBeat ? null : new BeatInfo(packed >> IndexBits, (int)(packed & IndexMask));
        }
    }

    public PlayerInfo Player
    {
        get
        {
            var packed = Volatile.Read(ref _player);
            return new PlayerInfo((int)(packed & TrackMask), packed >> (TrackBits + 1), ((packed >> TrackBits) & 1) != 0);
        }
    }

    internal void PublishPlayer(int trackId, long position, bool playing) =>
        Volatile.Write(ref _player, (position << (TrackBits + 1)) | (playing ? 1L << TrackBits : 0) | (trackId & TrackMask));

    internal void PublishFrames(long frames) => Volatile.Write(ref _renderedFrames, frames);

    internal void PublishMetronome(bool running)
    {
        Volatile.Write(ref _metronomeRunning, running ? 1 : 0);
        if (!running)
        {
            Volatile.Write(ref _lastBeat, NoBeat);
        }
    }

    internal void PublishBeat(long frame, int indexInBar) =>
        Volatile.Write(ref _lastBeat, (frame << IndexBits) | ((long)indexInBar & IndexMask));
}
