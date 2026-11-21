using Avalonia.Input;

namespace JustPractise.App.Shortcuts;

/// <summary>Was ein Tastenkürzel auslöst.</summary>
public enum ShortcutAction
{
    PlayPause,
    ToStart,
    SeekBack,
    SeekForward,
    TempoDown,
    TempoUp,
    VolumeUp,
    VolumeDown,
    MetronomeStartStop,
    MetronomeFaster,
    MetronomeSlower,
    MetronomeMuchFaster,
    MetronomeMuchSlower,
    OpenSettings,
}

/// <summary>
/// Die feste Standardbelegung der ersten Version (siehe funktionen.md, Abschnitt „Standardbelegung“).
/// </summary>
/// <remarks>
/// Benannte Tasten (Leertaste, Pfeile, Pos1, M) gelten über die Taste. <c>[</c> und <c>]</c> gelten über das
/// erzeugte Zeichen, weil sie je nach Tastaturbelegung auf anderen Tasten liegen (deutsch mit AltGr). <c>+</c> und
/// <c>−</c> gelten über die Taste, damit Umschalt dazu die großen Schritte gibt (auf der deutschen Tastatur ergäbe
/// Umschalt++ sonst <c>*</c>); der Ziffernblock zählt mit.
/// </remarks>
public static class Shortcut
{
    public static ShortcutAction? Match(Key key, KeyModifiers modifiers, string? symbol)
    {
        switch (symbol)
        {
            case "[":
                return ShortcutAction.TempoDown;
            case "]":
                return ShortcutAction.TempoUp;
        }

        var shift = modifiers == KeyModifiers.Shift;
        if (modifiers == KeyModifiers.Control && key == Key.OemComma)
        {
            return ShortcutAction.OpenSettings;
        }

        if (modifiers is not (KeyModifiers.None or KeyModifiers.Shift))
        {
            return null;
        }

        return key switch
        {
            Key.OemPlus or Key.Add => shift ? ShortcutAction.MetronomeMuchFaster : ShortcutAction.MetronomeFaster,
            Key.OemMinus or Key.Subtract => shift ? ShortcutAction.MetronomeMuchSlower : ShortcutAction.MetronomeSlower,
            _ when shift => null,
            Key.Space => ShortcutAction.PlayPause,
            Key.Home => ShortcutAction.ToStart,
            Key.Left => ShortcutAction.SeekBack,
            Key.Right => ShortcutAction.SeekForward,
            Key.Up => ShortcutAction.VolumeUp,
            Key.Down => ShortcutAction.VolumeDown,
            Key.M => ShortcutAction.MetronomeStartStop,
            _ => null,
        };
    }

    /// <summary>In einem Textfeld wirken nur Kürzel mit Strg; die anderen Tasten gehen in das Feld.</summary>
    public static bool WorksInTextInput(KeyModifiers modifiers) => modifiers.HasFlag(KeyModifiers.Control);
}
