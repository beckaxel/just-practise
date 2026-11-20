namespace JustPractise.Audio.Engine;

/// <summary>
/// Öffnet das echte Gerät und weicht auf einen Ersatz aus, wenn das nicht geht (z. B. kein Ausgabegerät
/// angeschlossen). Die App startet dann trotzdem, nur ohne Ton.
/// </summary>
public sealed class FallbackOutput(IAudioOutput primary, IAudioOutput fallback) : IAudioOutput
{
    private const int DefaultSampleRate = 48000;

    private IAudioOutput? _active;

    /// <summary>Der Grund, warum das echte Gerät nicht ging, sonst <c>null</c>.</summary>
    public Exception? Failure { get; private set; }

    public string? DeviceName => _active?.DeviceName;

    public bool HasDevice => _active?.HasDevice ?? false;

    public TimeSpan Period => (_active ?? primary).Period;

    public int MixSampleRate
    {
        get
        {
            try
            {
                return primary.MixSampleRate;
            }
#pragma warning disable CA1031 // Jeder Fehler des Geräts führt zum Ersatz; welche Ausnahme die Engine wirft, ist nicht festgelegt.
            catch (Exception)
#pragma warning restore CA1031
            {
                return DefaultSampleRate;
            }
        }
    }

    public void Open(int sampleRate, int channels, AudioCallback callback)
    {
        Close();
        try
        {
            primary.Open(sampleRate, channels, callback);
            _active = primary;
            Failure = null;
        }
#pragma warning disable CA1031 // siehe oben
        catch (Exception exception)
#pragma warning restore CA1031
        {
            Failure = exception;
            primary.Close();
            fallback.Open(sampleRate, channels, callback);
            _active = fallback;
        }
    }

    public void Close()
    {
        _active?.Close();
        _active = null;
    }

    public void Dispose()
    {
        Close();
        primary.Dispose();
        fallback.Dispose();
    }
}
