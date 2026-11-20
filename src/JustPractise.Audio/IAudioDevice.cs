namespace JustPractise.Audio;

/// <summary>Das Ausgabegerät, wie es die Statusleiste sieht.</summary>
public interface IAudioDevice
{
    /// <summary>Ein echtes Gerät gibt Ton aus. Sonst läuft die App ohne Ton weiter.</summary>
    bool IsAvailable { get; }

    /// <summary>Name des Geräts, soweit bekannt.</summary>
    string? Name { get; }

    /// <summary>Verspätete Callbacks seit dem Öffnen (Prüfpunkt 1).</summary>
    long LateCallbacks { get; }
}
