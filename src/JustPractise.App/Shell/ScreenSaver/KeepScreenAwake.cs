using Avalonia.Controls;

namespace JustPractise.App.Shell.ScreenSaver;

/// <summary>
/// Solange das Hauptfenster maximiert oder im Vollbild ist, gehen Bildschirmschoner und automatische Sperre nicht an.
/// Beim Üben gibt es oft lange keine Eingabe, obwohl man auf die App schaut.
/// </summary>
public sealed class KeepScreenAwake(IScreenSaverInhibitor inhibitor)
{
    private bool _inhibited;

    public void Attach(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);
        Update(window.WindowState);
        window.PropertyChanged += (_, e) =>
        {
            if (e.Property == Window.WindowStateProperty)
            {
                Update(window.WindowState);
            }
        };
        window.Closed += (_, _) => Update(WindowState.Normal);
    }

    public void Update(WindowState state)
    {
        var inhibit = state is WindowState.Maximized or WindowState.FullScreen;
        if (inhibit != _inhibited)
        {
            _inhibited = inhibit;
            inhibitor.Inhibit(inhibit);
        }
    }
}
