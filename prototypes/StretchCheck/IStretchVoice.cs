namespace JustPractise.Prototypes.StretchCheck;

/// <summary>
/// Ein Verfahren zum Time-Stretching, das aus einem <see cref="AudioClip"/> liest.
/// <see cref="Start"/> und <see cref="Render"/> laufen im Audio-Callback und legen keinen Speicher an.
/// </summary>
internal interface IStretchVoice : IDisposable
{
    string Name { get; }

    /// <summary>Nächster Frame des Clips, der gelesen wird.</summary>
    long Position { get; }

    /// <summary>Latenz zwischen Eingabe und Ausgabe in Frames, soweit bekannt.</summary>
    int LatencyFrames { get; }

    void Start(AudioClip clip, long frame, double tempo);

    void Render(AudioClip clip, Span<float> output, double tempo, bool loop);
}
