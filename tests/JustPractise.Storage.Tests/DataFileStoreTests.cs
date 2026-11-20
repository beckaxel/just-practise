using JustPractise.Core.DataFiles;
using JustPractise.Core.Pieces;
using JustPractise.Core.Settings;

namespace JustPractise.Storage.Tests;

public sealed class DataFileStoreTests : IDisposable
{
    private readonly TemporaryDirectory _directory = new();

    public void Dispose() => _directory.Dispose();

    private DataFileStore<SidecarFile> Sidecar(string name = "Blues.jps") => new(_directory.File(name), AppDataFiles.Sidecar);

    private static SidecarFile WithTempo(SidecarFile sidecar, double tempo) =>
        sidecar.WithSettings("Blues.mp3", FileSettings.Default.WithTempo(tempo));

    [Fact]
    public void UsesDefaultsForAMissingFileWithoutCreatingIt()
    {
        var result = Sidecar().Load();

        Assert.Equal(DataFileStatus.Ok, result.Status);
        Assert.Empty(result.Value.Files);
        Assert.Empty(Directory.EnumerateFileSystemEntries(_directory.Path));
    }

    [Fact]
    public void SavesAndLoadsAgain()
    {
        var store = Sidecar();
        store.Load();

        Assert.True(store.TrySave(WithTempo(new SidecarFile(), 0.8)));

        var loaded = Sidecar().Load();
        Assert.Equal(DataFileStatus.Ok, loaded.Status);
        Assert.Equal(0.8, loaded.Value.SettingsFor("Blues.mp3").EffectiveTempo);
    }

    [Fact]
    public void WritesUtf8WithoutByteOrderMarkAndLeavesNoTemporaryFile()
    {
        var store = Sidecar("Über den Sternen.jps");
        store.Load();
        store.TrySave(new SidecarFile().WithSettings("Über den Sternen.mp3", FileSettings.Default.WithVolumeDb(-2)));

        var bytes = File.ReadAllBytes(store.Path);
        Assert.Equal((byte)'{', bytes[0]);
        Assert.Contains("Über den Sternen.mp3", System.Text.Encoding.UTF8.GetString(bytes), StringComparison.Ordinal);
        Assert.Equal([store.Path], Directory.EnumerateFileSystemEntries(_directory.Path));
    }

    [Fact]
    public void OverwritesAnExistingFile()
    {
        var store = Sidecar();
        store.Load();
        store.TrySave(WithTempo(new SidecarFile(), 0.8));

        var loaded = store.Load().Value;
        Assert.True(store.TrySave(WithTempo(loaded, 0.6)));

        Assert.Equal(0.6, Sidecar().Load().Value.SettingsFor("Blues.mp3").EffectiveTempo);
    }

    [Fact]
    public void CreatesTheAppDataDirectory()
    {
        var location = new AppDataLocation(_directory.File(Path.Combine("config", "just-practise")));
        var store = location.Settings();
        store.Load();

        Assert.True(store.TrySave(new AppSettings { Language = new LanguageSettings { Ui = "de" } }));
        Assert.True(File.Exists(Path.Combine(location.Directory, "settings.json")));
    }

    [Fact]
    public void NeverOverwritesACorruptFile()
    {
        var path = _directory.Create("Blues.jps", "{ kaputt");
        var store = Sidecar();

        var result = store.Load();

        Assert.Equal(DataFileStatus.Corrupt, result.Status);
        Assert.False(store.TrySave(WithTempo(result.Value, 0.8)));
        Assert.Equal("{ kaputt", File.ReadAllText(path));
    }

    [Fact]
    public void NeverOverwritesAFileFromANewerVersion()
    {
        var path = _directory.Create("Blues.jps", """{ "format": 99, "files": {} }""");
        var store = Sidecar();

        var result = store.Load();

        Assert.Equal(DataFileStatus.Newer, result.Status);
        Assert.False(store.TrySave(WithTempo(result.Value, 0.8)));
        Assert.Equal("""{ "format": 99, "files": {} }""", File.ReadAllText(path));
    }

    [Fact]
    public void AllowsSavingAgainAfterTheFileWasRepaired()
    {
        var path = _directory.Create("Blues.jps", "{ kaputt");
        var store = Sidecar();
        store.Load();

        File.WriteAllText(path, """{ "format": 1 }""");
        var result = store.Load();

        Assert.True(store.TrySave(WithTempo(result.Value, 0.8)));
    }

    [Fact]
    public void RequiresLoadingBeforeSaving() =>
        Assert.Throws<InvalidOperationException>(() => Sidecar().TrySave(new SidecarFile()));

    [Fact]
    public async Task SerializesConcurrentSaves()
    {
        var store = Sidecar();
        store.Load();

        await Task.WhenAll(Enumerable.Range(50, 100).Select(percent =>
            Task.Run(() => store.TrySave(WithTempo(new SidecarFile(), percent / 100.0)), TestContext.Current.CancellationToken)));

        Assert.Equal(DataFileStatus.Ok, Sidecar().Load().Status);
        Assert.Single(Directory.EnumerateFileSystemEntries(_directory.Path));
    }

    [Fact]
    public void NamesTheSidecarAfterThePiece()
    {
        var store = AppDataLocation.Sidecar(_directory.Path, "02 August Song");

        Assert.Equal(_directory.File("02 August Song.jps"), store.Path);
    }

    [Fact]
    public void LocatesTheSystemAppDataFolder()
    {
        var expected = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData, Environment.SpecialFolderOption.DoNotVerify);

        Assert.Equal(Path.Combine(expected, "just-practise"), AppDataLocation.System.Directory);
    }
}
