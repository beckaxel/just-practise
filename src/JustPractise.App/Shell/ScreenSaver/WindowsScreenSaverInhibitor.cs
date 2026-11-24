using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace JustPractise.App.Shell.ScreenSaver;

/// <summary>
/// Unter Windows über <c>SetThreadExecutionState</c>: Solange gesetzt, schaltet Windows den Bildschirm nicht ab,
/// startet keinen Bildschirmschoner und sperrt deshalb auch nicht. Gilt für den aufrufenden Thread; die App ruft
/// immer vom UI-Thread auf. Endet der Prozess, gibt Windows den Zustand von selbst frei.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed partial class WindowsScreenSaverInhibitor : IScreenSaverInhibitor
{
    private const uint Continuous = 0x80000000;
    private const uint SystemRequired = 0x00000001;
    private const uint DisplayRequired = 0x00000002;

    // Schlägt der Aufruf fehl (Rückgabe 0), läuft der Bildschirmschoner wie gewohnt; mehr ist nicht zu tun.
    public void Inhibit(bool inhibit) =>
        _ = SetThreadExecutionState(inhibit ? Continuous | SystemRequired | DisplayRequired : Continuous);

    [LibraryImport("kernel32.dll")]
    private static partial uint SetThreadExecutionState(uint flags);
}
