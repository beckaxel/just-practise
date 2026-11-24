using JustPractise.App.FileBrowser;
using JustPractise.Core.DataFiles;
using JustPractise.Core.Settings;
using JustPractise.Storage;

namespace JustPractise.App.Tests.FileBrowser;

public sealed class FileBrowserViewModelTests : IAsyncDisposable
{
    private readonly TemporaryDirectory _directory = new();
    private readonly FakeFolderPicker _picker = new();
    private readonly List<DataFileKeeper<DeviceMemory>> _memories = [];

    public FileBrowserViewModelTests() => Directory.CreateDirectory(MusicPath);

    /// <summary>Erst warten, bis das Gedächtnis im Hintergrund geschrieben ist, dann den Ordner löschen.</summary>
    public async ValueTask DisposeAsync()
    {
        foreach (var memory in _memories)
        {
            await memory.FlushAsync();
        }

        _directory.Dispose();
    }

    private string MusicPath => _directory.File("Musik");

    private string Music(params string[] parts) => Path.Combine([MusicPath, .. parts]);

    private DataFileStore<DeviceMemory> DeviceStore() => new(_directory.File("device.json"), AppDataFiles.Device);

    private DataFileKeeper<DeviceMemory> Memory(DeviceMemory? value = null)
    {
        if (value is not null)
        {
            var store = DeviceStore();
            store.Load();
            store.TrySave(value);
        }

        var memory = new DataFileKeeper<DeviceMemory>(DeviceStore());
        _memories.Add(memory);
        return memory;
    }

    private DataFileKeeper<DeviceMemory> MemoryWithMusic(string? lastFolder = null) =>
        Memory(new DeviceMemory { MusicFolders = [MusicPath], LastFolder = lastFolder });

    private async Task<FileBrowserViewModel> StartAsync(DataFileKeeper<DeviceMemory> memory)
    {
        var browser = new FileBrowserViewModel(memory, _picker);
        await browser.StartAsync();
        return browser;
    }

    /// <summary>Die Zeilen als „Tiefe Art Name“, z. B. „1 Folder Jazz“, damit ein Vergleich den ganzen Baum zeigt.</summary>
    private static string[] Tree(FileBrowserViewModel browser) =>
        [.. browser.Rows.Select(row => $"{row.Depth} {row.Kind} {row.Name}{(row.HasChildren ? (row.IsExpanded ? " -" : " +") : "")}")];

    private static LibraryRow Row(FileBrowserViewModel browser, string name) => browser.Rows.Single(row => row.Name == name);

    [Fact]
    public async Task AsksForAMusicFolderOnTheFirstStart()
    {
        var browser = await StartAsync(Memory());

        Assert.True(browser.HasNoMusicFolders);
        Assert.Empty(browser.Rows);
        Assert.Empty(browser.Breadcrumbs);
        Assert.False(browser.IsLoading);
    }

    [Fact]
    public async Task AddsTheChosenMusicFolderAndExpandsIt()
    {
        _directory.Create(Path.Combine("Musik", "Blues.mp3"));
        var memory = Memory();
        var browser = await StartAsync(memory);
        _picker.Next = MusicPath + Path.DirectorySeparatorChar;

        await browser.AddMusicFolderCommand.ExecuteAsync(null);

        Assert.Equal([MusicPath], memory.Value.MusicFolders);
        Assert.Equal(["0 Folder Musik -", "1 File Blues"], Tree(browser));
        Assert.Equal(MusicPath, browser.Location);
        Assert.False(browser.HasNoMusicFolders);
        await memory.FlushAsync();
        Assert.Equal([MusicPath], DeviceStore().Load().Value.Library.ExpandedFolders);
    }

    [Fact]
    public async Task DoesNotAddTheSameMusicFolderTwiceOrAnythingWhenCancelled()
    {
        var memory = MemoryWithMusic();
        var browser = await StartAsync(memory);

        _picker.Next = MusicPath;
        await browser.AddMusicFolderCommand.ExecuteAsync(null);
        _picker.Next = null;
        await browser.AddMusicFolderCommand.ExecuteAsync(null);

        Assert.Equal([MusicPath], memory.Value.MusicFolders);
    }

    [Fact]
    public async Task ListsTheMusicFoldersAsRootsAndMarksMissingOnes()
    {
        _directory.Create(Path.Combine("Musik", "a.mp3"));
        var missing = _directory.File("Externe Platte");
        var browser = await StartAsync(Memory(new DeviceMemory { MusicFolders = [MusicPath, missing] }));

        Assert.Equal(["0 Folder Musik +", "0 Folder Externe Platte"], Tree(browser));
        Assert.True(Row(browser, "Musik").IsReachable);
        Assert.False(Row(browser, "Externe Platte").IsReachable);
    }

    [Fact]
    public async Task ExpandsFoldersInPlaceWithVirtualAndRealPieces()
    {
        _directory.Create(Path.Combine("Musik", "Jazz", "Autumn Leaves - Demo.mp3"));
        _directory.Create(Path.Combine("Musik", "Jazz", "Autumn Leaves - Playalong 80.mp3"));
        _directory.Create(Path.Combine("Musik", "Jazz", "Blues in F.mp3"));
        _directory.Create(Path.Combine("Musik", "Jazz", "Autumn Leaves.jps"));
        _directory.Create(Path.Combine("Musik", "Jazz", "Latin", "x.mp3"));
        Directory.CreateDirectory(Music("Jazz", "Leer"));
        _directory.Create(Path.Combine("Musik", "Etüde.mp3"));
        var browser = await StartAsync(MemoryWithMusic());

        await browser.ActivateCommand.ExecuteAsync(Row(browser, "Musik"));
        await browser.ActivateCommand.ExecuteAsync(Row(browser, "Jazz"));
        await browser.ActivateCommand.ExecuteAsync(Row(browser, "Autumn Leaves"));

        Assert.Equal(
            [
                "0 Folder Musik -",
                "1 Folder Jazz -",
                "2 Folder Latin +",
                "2 Folder Leer",
                "2 Piece Autumn Leaves -",
                "3 File Autumn Leaves - Demo.mp3",
                "3 File Autumn Leaves - Playalong 80.mp3",
                "2 File Blues in F",
                "1 File Etüde",
            ],
            Tree(browser));
        Assert.Equal(Music("Jazz", "Blues in F.mp3"), Row(browser, "Blues in F").File?.Path);
        Assert.Equal(2 * LibraryRow.IndentPerLevel, Row(browser, "Latin").Indent);
    }

    [Fact]
    public async Task CollapsesAndRestoresWhatWasExpandedBelow()
    {
        _directory.Create(Path.Combine("Musik", "Jazz", "Latin", "x.mp3"));
        var browser = await StartAsync(MemoryWithMusic());
        await browser.ActivateCommand.ExecuteAsync(Row(browser, "Musik"));
        await browser.ActivateCommand.ExecuteAsync(Row(browser, "Jazz"));
        await browser.ActivateCommand.ExecuteAsync(Row(browser, "Latin"));

        await browser.ActivateCommand.ExecuteAsync(Row(browser, "Musik"));
        Assert.Equal(["0 Folder Musik +"], Tree(browser));

        await browser.ActivateCommand.ExecuteAsync(Row(browser, "Musik"));
        Assert.Equal(["0 Folder Musik -", "1 Folder Jazz -", "2 Folder Latin -", "3 File x"], Tree(browser));
    }

    [Fact]
    public async Task OnlyRealPiecesArePlayable()
    {
        _directory.Create(Path.Combine("Musik", "Blues - Demo.mp3"));
        _directory.Create(Path.Combine("Musik", "Blues - Playalong.mp3"));
        var browser = await StartAsync(Memory(new DeviceMemory { MusicFolders = [MusicPath], Library = new LibraryState { ExpandedFolders = [MusicPath] } }));

        await browser.ActivateCommand.ExecuteAsync(Row(browser, "Blues"));
        Assert.Null(browser.SelectedFile);

        await browser.ActivateCommand.ExecuteAsync(Row(browser, "Blues - Playalong.mp3"));

        Assert.Equal(new PieceFile(MusicPath, "Blues", "Blues - Playalong.mp3"), browser.SelectedFile);
        Assert.True(Row(browser, "Blues - Playalong.mp3").IsSelected);
        Assert.False(Row(browser, "Blues - Demo.mp3").IsSelected);
        Assert.False(Row(browser, "Blues").IsSelected);

        await browser.ActivateCommand.ExecuteAsync(Row(browser, "Blues"));
        Assert.True(Row(browser, "Blues").IsSelected); // zugeklappt zeigt das Stück, dass eine seiner Dateien gewählt ist
    }

    [Fact]
    public async Task RestoresTheTreeAndTheSelectedFileOnTheNextStart()
    {
        _directory.Create(Path.Combine("Musik", "Jazz", "Blues - Demo.mp3"));
        _directory.Create(Path.Combine("Musik", "Jazz", "Blues - Playalong.mp3"));
        var memory = MemoryWithMusic();
        var browser = await StartAsync(memory);
        await browser.ActivateCommand.ExecuteAsync(Row(browser, "Musik"));
        await browser.ActivateCommand.ExecuteAsync(Row(browser, "Jazz"));
        await browser.ActivateCommand.ExecuteAsync(Row(browser, "Blues"));
        await browser.ActivateCommand.ExecuteAsync(Row(browser, "Blues - Playalong.mp3"));
        await memory.FlushAsync();

        LibraryRow? scrolledTo = null;
        var next = new FileBrowserViewModel(Memory(), _picker);
        next.ScrollRequested += (_, row) => scrolledTo = row;
        await next.StartAsync();

        Assert.Equal(Tree(browser), Tree(next));
        Assert.Equal(Music("Jazz", "Blues - Playalong.mp3"), next.SelectedFile?.Path);
        Assert.Same(Row(next, "Blues - Playalong.mp3"), scrolledTo);
        Assert.Equal(Music("Jazz"), next.Location);
        Assert.Equal(["Musik", "Jazz"], next.Breadcrumbs.Select(crumb => crumb.Name));
    }

    [Fact]
    public async Task ForgetsASelectedFileThatIsGone()
    {
        _directory.Create(Path.Combine("Musik", "a.mp3"));
        var browser = await StartAsync(Memory(new DeviceMemory
        {
            MusicFolders = [MusicPath],
            Library = new LibraryState { ExpandedFolders = [MusicPath], SelectedFile = Music("weg.mp3") },
        }));

        Assert.Null(browser.SelectedFile);
    }

    [Fact]
    public async Task ExpandsTheWayToTheLastFolderOfAnOlderVersion()
    {
        _directory.Create(Path.Combine("Musik", "Jazz", "a.mp3"));

        var browser = await StartAsync(MemoryWithMusic(Music("Jazz")));

        Assert.Equal(["0 Folder Musik -", "1 Folder Jazz -", "2 File a"], Tree(browser));
        Assert.Equal(Music("Jazz"), browser.Location);
    }

    [Fact]
    public async Task ShowsThePathOfTheChosenPlace()
    {
        _directory.Create(Path.Combine("Musik", "Jazz", "Latin", "x.mp3"));
        var browser = await StartAsync(Memory(new DeviceMemory { MusicFolders = [MusicPath], Library = new LibraryState { ExpandedFolders = [MusicPath] } }));
        Assert.Empty(browser.Breadcrumbs);

        await browser.ActivateCommand.ExecuteAsync(Row(browser, "Jazz"));
        await browser.ActivateCommand.ExecuteAsync(Row(browser, "Latin"));
        Assert.Equal(["Musik", "Jazz", "Latin"], browser.Breadcrumbs.Select(crumb => crumb.Name));
        Assert.True(browser.Breadcrumbs[0].IsFirst);
        Assert.False(browser.Breadcrumbs[1].IsFirst);

        await browser.ActivateCommand.ExecuteAsync(Row(browser, "x"));
        Assert.Equal(Music("Jazz", "Latin"), browser.Location);
    }

    [Fact]
    public async Task ABreadcrumbExpandsItsFolderAndScrollsToIt()
    {
        _directory.Create(Path.Combine("Musik", "Jazz", "Latin", "x.mp3"));
        var browser = await StartAsync(Memory(new DeviceMemory
        {
            MusicFolders = [MusicPath],
            LastFolder = Music("Jazz", "Latin"),
            Library = new LibraryState { ExpandedFolders = [MusicPath] },
        }));
        Assert.Equal(["0 Folder Musik -", "1 Folder Jazz +"], Tree(browser));
        LibraryRow? scrolledTo = null;
        browser.ScrollRequested += (_, row) => scrolledTo = row;

        await browser.OpenBreadcrumbCommand.ExecuteAsync(browser.Breadcrumbs[1]);

        Assert.Equal(["0 Folder Musik -", "1 Folder Jazz -", "2 Folder Latin +"], Tree(browser));
        Assert.Same(Row(browser, "Jazz"), scrolledTo);
        Assert.Equal(Music("Jazz"), browser.Location);
    }

    [Fact]
    public async Task RefreshKeepsTheTreeAndTheSelection()
    {
        _directory.Create(Path.Combine("Musik", "a.mp3"));
        var browser = await StartAsync(Memory(new DeviceMemory { MusicFolders = [MusicPath], Library = new LibraryState { ExpandedFolders = [MusicPath] } }));
        await browser.ActivateCommand.ExecuteAsync(Row(browser, "a"));
        _directory.Create(Path.Combine("Musik", "b.mp3"));

        await browser.RefreshCommand.ExecuteAsync(null);

        Assert.Equal(["0 Folder Musik -", "1 File a", "1 File b"], Tree(browser));
        Assert.True(Row(browser, "a").IsSelected);
    }

    [Fact]
    public async Task IgnoresAnExpandThatARebuildOvertook()
    {
        _directory.Create(Path.Combine("Musik", "A", "a.mp3"));
        var browser = await StartAsync(Memory(new DeviceMemory { MusicFolders = [MusicPath], Library = new LibraryState { ExpandedFolders = [MusicPath] } }));

        var expand = browser.ActivateCommand.ExecuteAsync(Row(browser, "A"));
        var refresh = browser.RefreshCommand.ExecuteAsync(null);
        await Task.WhenAll(expand, refresh);

        Assert.Equal(1, browser.Rows.Count(row => row.Name == "a"));
        Assert.False(browser.IsLoading);
    }

    [Fact]
    public async Task RemovesAMusicFolderAndClearsThePathInIt()
    {
        var other = _directory.File("Noten");
        Directory.CreateDirectory(other);
        _directory.Create(Path.Combine("Musik", "Jazz", "a.mp3"));
        var memory = Memory(new DeviceMemory { MusicFolders = [MusicPath, other], LastFolder = Music("Jazz") });
        var browser = await StartAsync(memory);

        await browser.RemoveMusicFolderCommand.ExecuteAsync(MusicPath + Path.DirectorySeparatorChar);

        Assert.Equal([other], browser.MusicFolders);
        Assert.Equal([other], memory.Value.MusicFolders);
        Assert.Equal(["0 Folder Noten"], Tree(browser));
        Assert.Null(browser.Location);
        Assert.Empty(browser.Breadcrumbs);
        Assert.True(Directory.Exists(MusicPath));
    }

    [Fact]
    public async Task ShowsTheHintAgainWhenTheLastMusicFolderIsRemoved()
    {
        var browser = await StartAsync(MemoryWithMusic());

        await browser.RemoveMusicFolderCommand.ExecuteAsync(MusicPath);

        Assert.Empty(browser.MusicFolders);
        Assert.True(browser.HasNoMusicFolders);
        Assert.Empty(browser.Rows);
    }

    private sealed class FakeFolderPicker : IFolderPicker
    {
        public string? Next { get; set; }

        public Task<string?> PickFolderAsync() => Task.FromResult(Next);
    }
}
