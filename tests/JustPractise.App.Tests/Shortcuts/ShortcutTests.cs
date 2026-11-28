using Avalonia.Input;
using JustPractise.App.Shortcuts;

namespace JustPractise.App.Tests.Shortcuts;

public sealed class ShortcutTests
{
    [Theory]
    [InlineData(Key.Space, KeyModifiers.None, " ", ShortcutAction.PlayPause)]
    [InlineData(Key.Home, KeyModifiers.None, null, ShortcutAction.ToStart)]
    [InlineData(Key.Left, KeyModifiers.None, null, ShortcutAction.SeekBack)]
    [InlineData(Key.Right, KeyModifiers.None, null, ShortcutAction.SeekForward)]
    [InlineData(Key.Up, KeyModifiers.None, null, ShortcutAction.VolumeUp)]
    [InlineData(Key.Down, KeyModifiers.None, null, ShortcutAction.VolumeDown)]
    [InlineData(Key.M, KeyModifiers.None, "m", ShortcutAction.MetronomeStartStop)]
    [InlineData(Key.OemPlus, KeyModifiers.None, "+", ShortcutAction.MetronomeFaster)]
    [InlineData(Key.Add, KeyModifiers.None, "+", ShortcutAction.MetronomeFaster)]
    [InlineData(Key.OemPlus, KeyModifiers.Shift, "*", ShortcutAction.MetronomeMuchFaster)]
    [InlineData(Key.OemMinus, KeyModifiers.None, "-", ShortcutAction.MetronomeSlower)]
    [InlineData(Key.Subtract, KeyModifiers.None, "-", ShortcutAction.MetronomeSlower)]
    [InlineData(Key.OemMinus, KeyModifiers.Shift, "_", ShortcutAction.MetronomeMuchSlower)]
    [InlineData(Key.OemComma, KeyModifiers.Control, ",", ShortcutAction.OpenSettings)]
    public void FollowsTheDefaultBindings(Key key, KeyModifiers modifiers, string? symbol, ShortcutAction expected) =>
        Assert.Equal(expected, Shortcut.Match(key, modifiers, symbol));

    [Theory]
    [InlineData(Key.OemOpenBrackets, KeyModifiers.None, "[", ShortcutAction.TempoDown)] // US
    [InlineData(Key.OemCloseBrackets, KeyModifiers.None, "]", ShortcutAction.TempoUp)]
    [InlineData(Key.D8, KeyModifiers.Control | KeyModifiers.Alt, "[", ShortcutAction.TempoDown)] // deutsch: AltGr+8
    [InlineData(Key.D9, KeyModifiers.Control | KeyModifiers.Alt, "]", ShortcutAction.TempoUp)]
    public void FindsBracketsOnEveryKeyboardLayout(Key key, KeyModifiers modifiers, string symbol, ShortcutAction expected) =>
        Assert.Equal(expected, Shortcut.Match(key, modifiers, symbol));

    [Theory]
    [InlineData(Key.Space, KeyModifiers.Shift, " ")]
    [InlineData(Key.M, KeyModifiers.Control, "m")]
    [InlineData(Key.Left, KeyModifiers.Alt, null)]
    [InlineData(Key.OemComma, KeyModifiers.None, ",")]
    [InlineData(Key.A, KeyModifiers.None, "a")]
    public void IgnoresOtherKeys(Key key, KeyModifiers modifiers, string? symbol) =>
        Assert.Null(Shortcut.Match(key, modifiers, symbol));

    [Fact]
    public void LetsOnlyControlShortcutsThroughInTextInput()
    {
        Assert.True(Shortcut.WorksInTextInput(KeyModifiers.Control));
        Assert.False(Shortcut.WorksInTextInput(KeyModifiers.None));
        Assert.False(Shortcut.WorksInTextInput(KeyModifiers.Shift));
    }
}
