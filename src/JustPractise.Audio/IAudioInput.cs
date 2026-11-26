using JustPractise.Audio.Tuner;

namespace JustPractise.Audio;

/// <summary>Das Eingabegerät für das Stimmgerät (Grenze zum System). Offen ist es nur, solange das Stimmgerät an ist.</summary>
public interface IAudioInput
{
    /// <summary>Die Abtastrate, mit der das Gerät geöffnet wird.</summary>
    int SampleRate { get; }

    /// <summary>Die Namen der Eingabegeräte; leer, wenn das System keine meldet.</summary>
    IReadOnlyList<string> DeviceNames();

    /// <summary>
    /// Öffnet das Gerät (<c>null</c> oder ein unbekannter Name: das Standardgerät) und schreibt die Samples mono in
    /// <paramref name="ring"/>. Gibt false zurück, wenn es sich nicht öffnen ließ, z. B. ohne Mikrofon oder ohne Freigabe.
    /// </summary>
    bool Open(string? deviceName, InputRing ring);

    void Close();
}
