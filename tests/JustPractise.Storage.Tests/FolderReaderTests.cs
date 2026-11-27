namespace JustPractise.Storage.Tests;

public sealed class FolderReaderTests : IDisposable
{
    private readonly TemporaryDirectory _directory = new();

    public void Dispose() => _directory.Dispose();

    [Fact]
    public void GroupsOnlySupportedAudioFiles()
    {
        foreach (var name in new[]
        {
            "Balmoral.mp3", "Balmoral - Ces.pdf", "Balmoral - Ces.mscz", "Balmoral.jps",
            "Jingle Bells.MP3", "Jingle Bells - Metronom 100.ogg", "Telstar.flac", "Telstar.wav", "Blue Bossa100.m4a",
            "Konzert.mp4",
        })
        {
            _directory.Create(name);
        }

        var content = FolderReader.Read(_directory.Path);

        Assert.True(content.IsReachable);
        Assert.Equal(["Balmoral", "Blue Bossa", "Jingle Bells", "Telstar"], content.Pieces.Select(piece => piece.Title));
        Assert.Equal(["Balmoral.mp3"], content.Pieces[0].Files);
        Assert.Equal(["Blue Bossa100.m4a"], content.Pieces[1].Files);
        Assert.Equal(["Jingle Bells - Metronom 100.ogg", "Jingle Bells.MP3"], content.Pieces[2].Files);
        Assert.Equal(["Telstar.flac", "Telstar.wav"], content.Pieces[3].Files);
    }

    [Fact]
    public void ListsSubfoldersSortedWithoutHiddenOnes()
    {
        _directory.Create(Path.Combine("Saxophonunterricht", "a.mp3"));
        _directory.Create(Path.Combine("Eigenes", "b.mp3"));
        _directory.Create(Path.Combine("klavierunterricht", "c.mp3"));
        _directory.Create(Path.Combine(".mscbackup", "d.mscz"));
        _directory.Create(".hidden.mp3");

        var content = FolderReader.Read(_directory.Path);

        Assert.Equal(["Eigenes", "klavierunterricht", "Saxophonunterricht"], content.Subfolders);
        Assert.Empty(content.Pieces);
    }

    [Fact]
    public void ReadsOnlyTheFolderItself()
    {
        _directory.Create(Path.Combine("LyraGruppe", "Telstar.mp3"));

        Assert.Empty(FolderReader.Read(_directory.Path).Pieces);
    }

    [Fact]
    public void ReportsAMissingFolderAsUnreachable()
    {
        var content = FolderReader.Read(_directory.File("gibt es nicht"));

        Assert.False(content.IsReachable);
        Assert.Empty(content.Subfolders);
        Assert.Empty(content.Pieces);
    }
}
