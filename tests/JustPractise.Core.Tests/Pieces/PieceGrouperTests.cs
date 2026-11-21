using JustPractise.Core.Pieces;

namespace JustPractise.Core.Tests.Pieces;

// Die Dateinamen stammen größtenteils aus einer echten Übe-Sammlung.
public sealed class PieceGrouperTests
{
    private static Dictionary<string, string[]> Group(params string[] fileNames) =>
        PieceGrouper.Group(fileNames).ToDictionary(piece => piece.Title, piece => piece.Files.ToArray());

    [Fact]
    public void GroupsTheExampleFromTheConcept()
    {
        var pieces = Group("Autumn Leaves - Demo.mp3", "Autumn Leaves - Playalong 80.mp3", "Autumn Leaves.mp3", "Blues.mp3", "Blues in F.mp3");

        Assert.Equal(["Autumn Leaves - Demo.mp3", "Autumn Leaves - Playalong 80.mp3", "Autumn Leaves.mp3"], pieces["Autumn Leaves"]);
        Assert.Equal(["Blues.mp3"], pieces["Blues"]);
        Assert.Equal(["Blues in F.mp3"], pieces["Blues in F"]);
    }

    [Fact]
    public void GroupsAPlayalongWithADifferentTrackNumber()
    {
        var pieces = Group("02 August Song.mp3", "03 August Song Play-Along.mp3", "04 Little Waves.mp3", "05 Little Waves Play-Along.mp3");

        Assert.Equal(2, pieces.Count);
        Assert.Equal(["02 August Song.mp3", "03 August Song Play-Along.mp3"], pieces["02 August Song"]);
        Assert.Equal(["04 Little Waves.mp3", "05 Little Waves Play-Along.mp3"], pieces["04 Little Waves"]);
    }

    [Fact]
    public void KeepsEqualNamesWithDifferentNumbersApart()
    {
        var pieces = Group("003 - Bourrée.mp3", "006 - Bourrée.mp3", "001 PianoFitness.mp3", "002 PianoFitness.mp3");

        Assert.Equal(["001 PianoFitness", "002 PianoFitness", "003 - Bourrée", "006 - Bourrée"], pieces.Keys.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void KeepsPlaceholderNamesApart()
    {
        var pieces = Group("01 Titel 01.mp3", "02 Titel 02.mp3", "01_Audio_Titel.mp3", "02_Audio_Titel.mp3");

        Assert.Equal(4, pieces.Count);
    }

    [Fact]
    public void RecognizesTheVariousTrackNumberStyles()
    {
        var pieces = Group(
            "45. Im schönsten Wiesengrunde.mp3",
            "45. Im schönsten Wiesengrunde - Play Along.mp3",
            "2-02 92. Etüde C-Dur.mp3",
            "2-07 97. Etüde C-Dur.mp3",
            "Track 06 Track 06 Kap 2 Akkorde im Quartenzirkel Eb.mp3");

        Assert.Equal(2, pieces["45. Im schönsten Wiesengrunde"].Length);
        Assert.Contains("2-02 92. Etüde C-Dur", pieces.Keys);
        Assert.Contains("2-07 97. Etüde C-Dur", pieces.Keys);
        Assert.Contains("Track 06 Track 06 Kap 2 Akkorde im Quartenzirkel", pieces.Keys);
    }

    [Fact]
    public void GroupsRecordingsInDifferentTemposAndVersions()
    {
        var pieces = Group(
            "Track 09 Kap 3 Ungar Tanz Bb 80 Instr.mp3",
            "Track 10 Kap 3 Ungar Tanz Bb 80 Bck.mp3",
            "Track 11 Kap 3 Ungar Tanz Bb 100 Instr.mp3",
            "Track 12 Kap 3 Ungar Tanz Bb 100 Bck.mp3",
            "Track 07 Kap 2 Air Bb 70 Instr.mp3");

        Assert.Equal(4, pieces["Track 09 Kap 3 Ungar Tanz"].Length);
        Assert.Single(pieces["Track 07 Kap 2 Air"]);
    }

    [Fact]
    public void SeparatesGluedTemposAndUnits()
    {
        var pieces = Group(
            "Blue Bossa100.mp3",
            "Blue Bossa - playalong.mp3",
            "baby mine full version_60bpm.mp3",
            "baby mine no pn_60bpm.mp3",
            "Titel 06 Blues in F 120 T.mp3",
            "Titel 10 Blues in F 140 T.mp3");

        Assert.Equal(["Blue Bossa - playalong.mp3", "Blue Bossa100.mp3"], pieces["Blue Bossa"]);
        Assert.Equal(2, pieces["baby mine"].Length);
        Assert.Equal(2, pieces["Titel 06 Blues in F"].Length);
    }

    [Fact]
    public void KeepsSmallNumbersAsPartOfTheTitle()
    {
        var pieces = Group("Track 08 Kap 2 Impro 4 80 Eb.mp3", "Track 09 Kap 2 Impro 5 80 Eb.mp3", "Trommlermarsch 01.mp3", "Trommlermarsch 02.mp3");

        Assert.Equal(4, pieces.Count);
    }

    [Fact]
    public void StripsBareTranspositionsButKeepsAKeyAfterIn()
    {
        var pieces = Group(
            "Titel 01 Blues in F 100.mp3",
            "Titel 03 Blues in Bb 100.mp3",
            "Workout 1_01_01 Alt.mp3",
            "Workout 1_01_01 Tenor.mp3",
            "All of me Ab Dur 100bpm bs_drs_git.mp3",
            "All of me Ab Dur 124bpm bs_drs_git.mp3");

        Assert.Single(pieces["Titel 01 Blues in F"]);
        Assert.Single(pieces["Titel 03 Blues in Bb"]);
        Assert.Equal(2, pieces["Workout 1_01_01"].Length);
        Assert.Equal(2, pieces["All of me Ab Dur"].Length);
    }

    [Fact]
    public void MergesAfterAHardSeparatorOnlyIfThePlainTitleExists()
    {
        var pieces = Group(
            "Hot Cross Buns.mp3",
            "Hot Cross Buns - Metronom.mp3",
            "Hot Cross Buns - 01.mp3",
            "Hot Cross Buns - 01 - Metronom.mp3",
            "Franz Titscher - Kinderlieder.mp3",
            "Franz Titscher - Weihnachtslieder.mp3");

        Assert.Equal(4, pieces["Hot Cross Buns"].Length);
        Assert.Contains("Franz Titscher - Kinderlieder", pieces.Keys);
        Assert.Contains("Franz Titscher - Weihnachtslieder", pieces.Keys);
    }

    [Fact]
    public void MergesChainedHardSeparators()
    {
        var pieces = Group("Balmoral.mp3", "Balmoral - Ces.mp3", "Balmoral - C - Fis-Dur.mp3", "Balmoral - C - Ges-Dur.mp3");

        Assert.Equal(4, Assert.Single(pieces).Value.Length);
    }

    [Fact]
    public void IgnoresCaseAndApostrophes()
    {
        var pieces = Group("Aint no Sunshine orig.mp3", "Aint no sunshine playback.mp3", "Ain't No Sunshine.mp3", "01 Basie s Blues.mp3");

        Assert.Equal(3, pieces["Ain't No Sunshine"].Length);
        Assert.Contains("01 Basie s Blues", pieces.Keys);
    }

    [Fact]
    public void HandlesSuffixesInBrackets()
    {
        var pieces = Group(
            "02 Nr.6 (mit Sax).mp3",
            "03 Nr.6 (ohne Sax).mp3",
            "01 Milwaukee Avenue (Sax Demonstration Track).mp3",
            "02 Milwaukee Avenue (Rhythm Section Only).mp3",
            "Hot and Humid [without piano].mp3",
            "Hot And Humid combo altoxax.mp3",
            "104. Walzer (Dornröschen).mp3");

        Assert.Equal(2, pieces["02 Nr.6"].Length);
        Assert.Equal(2, pieces["01 Milwaukee Avenue"].Length);
        Assert.Single(pieces["Hot and Humid"]);
        Assert.Single(pieces["Hot And Humid combo altoxax"]);
        Assert.Single(pieces["104. Walzer (Dornröschen)"]);
    }

    [Fact]
    public void UsesTheWholeNameIfOnlySuffixesRemain()
    {
        var pieces = Group("Titel 01 click 48.mp3", "Titel 02 click 52.mp3", "Titel 01 Metronom Klick Tempo 66.mp3");

        Assert.Equal(["Titel 01 click 48", "Titel 01 Metronom Klick Tempo 66", "Titel 02 click 52"], pieces.Keys.Order(StringComparer.OrdinalIgnoreCase));
    }

    [Fact]
    public void TreatsUnicodeFormsAsEqual()
    {
        var decomposed = "Bourrée.mp3";
        var pieces = Group("Bourrée.mp3", decomposed.Replace(".mp3", " - Play Along.mp3", StringComparison.Ordinal));

        Assert.Equal(2, Assert.Single(pieces).Value.Length);
    }

    [Fact]
    public void DoesNotTakeTheTrackNumberForATempo()
    {
        var pieces = Group("30 Titel 30.mp3", "31 Titel 31.mp3", "32 Titel 32.mp3");

        Assert.Equal(3, pieces.Count);
    }

    [Fact]
    public void StripsOnlyOneTransposition()
    {
        var pieces = Group("Track 59 Kap 12 Skalen Akk C Eb.mp3", "Track 61 Kap 12 Skalen Akk Bb Eb.mp3", "Track 62 Kap 12 Skalen Akk Eb Eb.mp3");

        Assert.Equal(3, pieces.Count);
    }

    [Fact]
    public void TakesTheTitleFromTheFirstFile()
    {
        var pieces = Group("Track 13 Kap 3 First Blues bckgr 104.mp3", "Track 12 Kap 3 First Blues instr 104Eb.mp3");

        Assert.Equal("Track 12 Kap 3 First Blues", Assert.Single(pieces).Key);
    }
}
