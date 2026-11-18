using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace JustPractise.Core.Metronome;

/// <summary>Eine Taktart wie 4/4 oder 7/8.</summary>
public readonly record struct Meter(int Beats, int NoteValue)
{
    public static Meter ThreeFour { get; } = new(3, 4);

    public static Meter FourFour { get; } = new(4, 4);

    public static bool TryParse([NotNullWhen(true)] string? text, out Meter meter)
    {
        meter = default;
        var parts = text?.Split('/');
        if (parts is not [var beatsText, var noteText]
            || !int.TryParse(beatsText, NumberStyles.None, CultureInfo.InvariantCulture, out var beats)
            || !int.TryParse(noteText, NumberStyles.None, CultureInfo.InvariantCulture, out var noteValue)
            || beats is < 1 or > 32
            || noteValue is not (1 or 2 or 4 or 8 or 16 or 32))
        {
            return false;
        }

        meter = new Meter(beats, noteValue);
        return true;
    }

    public override string ToString() => string.Create(CultureInfo.InvariantCulture, $"{Beats}/{NoteValue}");
}
