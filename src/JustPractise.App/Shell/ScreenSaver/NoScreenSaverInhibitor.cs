namespace JustPractise.App.Shell.ScreenSaver;

/// <summary>Für Systeme ohne eigene Umsetzung (bisher macOS): Der Bildschirmschoner läuft wie gewohnt.</summary>
public sealed class NoScreenSaverInhibitor : IScreenSaverInhibitor
{
    public void Inhibit(bool inhibit)
    {
    }
}
