using JustPractise.App.FileBrowser;

namespace JustPractise.App.Tests.FileBrowser;

public sealed class FolderPathsTests
{
    private static readonly string Base = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "jp-paths"));

    private static string P(params string[] parts) => Path.Combine([Base, .. parts]);

    [Fact]
    public void FindsTheDeepestMusicFolderThatContainsThePath()
    {
        string[] roots = [P("Musik"), P("Musik", "Jazz")];

        Assert.Equal(P("Musik", "Jazz"), FolderPaths.FindRoot(roots, P("Musik", "Jazz", "Standards")));
        Assert.Equal(P("Musik"), FolderPaths.FindRoot(roots, P("Musik", "Klassik")));
        Assert.Equal(P("Musik"), FolderPaths.FindRoot(roots, P("Musik") + Path.DirectorySeparatorChar));
    }

    [Fact]
    public void DoesNotTreatASiblingWithTheSamePrefixAsInside()
    {
        Assert.Null(FolderPaths.FindRoot([P("Musik")], P("Musik2", "Blues")));
        Assert.Null(FolderPaths.FindRoot([P("Musik")], Base));
    }

    [Fact]
    public void SplitsThePathBelowTheMusicFolder()
    {
        Assert.Equal(["Jazz", "Standards"], FolderPaths.Segments(P("Musik"), P("Musik", "Jazz", "Standards")));
        Assert.Empty(FolderPaths.Segments(P("Musik"), P("Musik")));
    }

    [Fact]
    public void NamesARootDriveByItsPath()
    {
        var root = Path.GetPathRoot(Base)!;

        Assert.Equal(root, FolderPaths.DisplayName(root));
        Assert.Equal("Musik", FolderPaths.DisplayName(P("Musik") + Path.DirectorySeparatorChar));
    }
}
