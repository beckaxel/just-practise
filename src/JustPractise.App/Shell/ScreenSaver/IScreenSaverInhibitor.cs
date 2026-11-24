namespace JustPractise.App.Shell.ScreenSaver;

/// <summary>Hält Bildschirmschoner, Abschalten des Bildschirms und automatische Sperre auf (Grenze zum System).</summary>
public interface IScreenSaverInhibitor
{
    /// <summary><c>true</c> hält sie auf, <c>false</c> gibt sie wieder frei. Wiederholte Aufrufe ändern nichts.</summary>
    void Inhibit(bool inhibit);
}
