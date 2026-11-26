using JustPractise.Core.Tuner;

namespace JustPractise.Core.Tests.Tuner;

public sealed class TunedNoteTests
{
    [Theory]
    [InlineData(440.0, 69, "A", 4)]
    [InlineData(261.6256, 60, "C", 4)]
    [InlineData(41.2034, 28, "E", 1)]
    [InlineData(4186.009, 108, "C", 8)]
    [InlineData(233.0819, 58, "A♯", 3)]
    public void FindsTheNearestNoteAndOctave(double frequency, int midi, string german, int octave)
    {
        var note = TunedNote.FromFrequency(frequency);

        Assert.Equal(midi, note.Midi);
        Assert.Equal(german, note.Name(NoteNaming.German, Accidentals.Sharp));
        Assert.Equal(octave, note.Octave);
        Assert.InRange(note.Cents, -0.1, 0.1);
    }

    [Fact]
    public void MeasuresTheDeviationInCents()
    {
        var sharp = TunedNote.FromFrequency(440 * Math.Pow(2, 12 / 1200.0));
        var flat = TunedNote.FromFrequency(440 * Math.Pow(2, -30 / 1200.0));

        Assert.Equal(69, sharp.Midi);
        Assert.Equal(12, sharp.Cents, 6);
        Assert.Equal("+12", sharp.CentsText);
        Assert.Equal("−30", flat.CentsText);
        Assert.Equal("0", TunedNote.FromFrequency(440).CentsText);
    }

    [Fact]
    public void FollowsTheReferencePitch()
    {
        Assert.Equal(0, TunedNote.FromFrequency(442, 442).Cents, 6);
        Assert.Equal(7.85, TunedNote.FromFrequency(442, 440).Cents, 2);
    }

    [Fact]
    public void NamesNotesInGermanAndEnglishWithSharpsOrFlats()
    {
        var bFlat = new TunedNote(70, 0);
        var b = new TunedNote(71, 0);

        Assert.Equal("A♯", bFlat.Name(NoteNaming.German, Accidentals.Sharp));
        Assert.Equal("B", bFlat.Name(NoteNaming.German, Accidentals.Flat));
        Assert.Equal("H", b.Name(NoteNaming.German, Accidentals.Flat));
        Assert.Equal("A♯", bFlat.Name(NoteNaming.English, Accidentals.Sharp));
        Assert.Equal("B♭", bFlat.Name(NoteNaming.English, Accidentals.Flat));
        Assert.Equal("B", b.Name(NoteNaming.English, Accidentals.Sharp));
        Assert.Equal("E♭", new TunedNote(63, 0).Name(NoteNaming.German, Accidentals.Flat));
        Assert.Equal("C♯", new TunedNote(61, 0).Name(NoteNaming.German, Accidentals.Sharp));
    }

    [Theory]
    [InlineData(0, "C")] // klingend
    [InlineData(2, "D")] // in B: klingendes C steht als D in den Noten
    [InlineData(9, "A")] // in Es
    [InlineData(7, "G")] // in F
    [InlineData(3, "D♯")] // in A
    public void ShowsTheWrittenNoteOfATransposingInstrument(int semitones, string written)
    {
        var sounding = TunedNote.FromFrequency(261.6256 * Math.Pow(2, 5 / 1200.0));

        var note = sounding.Written(semitones);

        Assert.Equal(written, note.Name(NoteNaming.German, Accidentals.Sharp));
        Assert.Equal(sounding.Cents, note.Cents);
    }
}
