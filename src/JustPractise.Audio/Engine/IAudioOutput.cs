namespace JustPractise.Audio.Engine;

/// <summary>Berechnet einen Block. Der Puffer ist interleaved und muss ganz beschrieben werden.</summary>
public delegate void AudioCallback(Span<float> buffer);

/// <summary>
/// Das Ausgabegerät, die Grenze zur Audio-Engine. Ruft auf seinem Audio-Thread regelmäßig den Callback auf.
/// </summary>
public interface IAudioOutput : IDisposable
{
    /// <summary>Name des Geräts für die Anzeige, solange es offen ist.</summary>
    string? DeviceName { get; }

    /// <summary>Ein echtes Gerät ist offen und gibt Ton aus.</summary>
    bool HasDevice { get; }

    /// <summary>Die Periode, in der das Gerät den Callback aufruft (eingestellte Pufferlänge).</summary>
    TimeSpan Period { get; }

    /// <summary>
    /// Die Rate, mit der das Standardgerät im geteilten Modus mischt. Mit dieser Rate geöffnet, rechnet weder die
    /// Audio-Engine noch das System um, und die Callbacks kommen gleichmäßig.
    /// </summary>
    int MixSampleRate { get; }

    /// <summary>Öffnet das Standard-Ausgabegerät und startet die Callbacks. Ein offenes Gerät wird vorher geschlossen.</summary>
    void Open(int sampleRate, int channels, AudioCallback callback);

    /// <summary>Hält die Callbacks an und schließt das Gerät. Danach läuft kein Callback mehr.</summary>
    void Close();
}

/// <summary>Decodiert eine Datei ganz in Blöcke, die Grenze zum Decoder der Audio-Engine.</summary>
public interface IAudioDecoder
{
    /// <inheritdoc cref="Player.FileDecoder.Decode"/>
    void Decode(string path, Player.BlockPool pool, Action<Player.DecodedAudio> opened, CancellationToken cancellationToken);
}
