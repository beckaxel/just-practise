namespace JustPractise.Audio.Player;

/// <summary>
/// Alles, was der Audio-Thread zum Abspielen einer Datei braucht, vorab außerhalb des Audio-Threads angelegt:
/// die decodierte Datei, der Stretcher und die Arbeitspuffer.
/// </summary>
/// <remarks>
/// Der Audio-Thread gibt eine ersetzte Datei über eine Rückgabe-Warteschlange zurück. Erst danach
/// darf sie mit <see cref="Dispose"/> freigegeben werden; ihre Blöcke gehen zurück in den Pool.
/// </remarks>
public sealed class LoadedTrack : IDisposable
{
    /// <summary>So viele Frames berechnet der Stretcher höchstens auf einmal.</summary>
    public const int MaxChunkFrames = 2048;

    private const double FadeSeconds = 0.005;

    public LoadedTrack(int id, DecodedAudio audio, StretchVoice stretch)
    {
        Id = id;
        Audio = audio;
        Stretch = stretch;
        var seekFrames = stretch.OutputSeekLength(stretch.MaxRate);
        var chunkInputFrames = (int)Math.Ceiling(MaxChunkFrames * stretch.MaxRate) + 1;
        Input = new float[Math.Max(seekFrames, chunkInputFrames) * audio.Channels];
        Output = new float[MaxChunkFrames * audio.Channels];
        FadeFrames = Math.Max(1, (int)(FadeSeconds * stretch.OutputRate));
        Smoothing = (float)(1 - Math.Exp(-1 / (FadeSeconds * stretch.OutputRate)));
    }

    /// <summary>Kennung, damit die Oberfläche Status und Datei einander zuordnen kann.</summary>
    public int Id { get; }

    public DecodedAudio Audio { get; }

    public StretchVoice Stretch { get; }

    /// <summary>Das Decodieren im Hintergrund. Freigegeben wird erst, wenn es fertig oder abgebrochen ist.</summary>
    public Task Decoding { get; init; } = Task.CompletedTask;

    internal float[] Input { get; }

    internal float[] Output { get; }

    /// <summary>Länge des Ein- und Ausblendens in Ausgabe-Frames beim Starten, Anhalten und Springen, gegen Knacksen.</summary>
    internal int FadeFrames { get; }

    /// <summary>Faktor für das gleitende Angleichen der Lautstärke pro Ausgabe-Frame.</summary>
    internal float Smoothing { get; }

    public void Dispose()
    {
        Stretch.Dispose();
        Audio.Release();
    }
}
