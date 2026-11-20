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

    [Fact]
    public async Task AsksForAMusicFolderOnTheFirstStart()
    {
        var browser = await StartAsync(Memory());

        Assert.True(browser.IsTop);
        Assert.True(browser.HasNoMusicFolders);
        Assert.Empty(browser.Folders);
        Assert.Equal([new BreadcrumbItem(null, null)], browser.Breadcrumbs);
        Assert.False(browser.IsLoading);
    }

    [Fact]
    public async Task AddsTheChosenMusicFolderAndOpensIt()
    {
        var memory = Memory();
        var browser = await StartAsync(memory);
        _picker.Next = MusicPath + Path.DirectorySeparatorChar;

        await browser.AddMusicFolderCommand.ExecuteAsync(null);

        Assert.Equal([MusicPath], memory.Value.MusicFolders);
        Assert.Equal(MusicPath, browser.Location);
        Assert.False(browser.HasNoMusicFolders);
        await memory.FlushAsync();
        Assert.Equal(MusicPath, DeviceStore().Load().Value.LastFolder);
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
    public async Task ListsTheMusicFoldersOnTheTopLevelAndMarksMissingOnes()
    {
        var missing = _directory.File("Externe Platte");
        var browser = await StartAsync(Memory(new DeviceMemory { MusicFolders = [MusicPath, missing] }));

        Assert.Equal(
            [new FolderItem("Musik", MusicPath, true), new FolderItem("Externe Platte", missing, false)],
            browser.Folders);
        Assert.False(browser.HasNoMusicFolders);
    }

    [Fact]
    public async Task ShowsSubfoldersAndGroupedPiecesWithBreadcrumbs()
    {
        _directory.Create(Path.Combine("Musik", "Jazz", "Standards", "Autumn Leaves - Demo.mp3"));
        _directory.Create(Path.Combine("Musik", "Jazz", "Standards", "Autumn Leaves - Playalong 80.mp3"));
        _directory.Create(Path.Combine("Musik", "Jazz", "Standards", "Blues in F.mp3"));
        _directory.Create(Path.Combine("Musik", "Jazz", "Standards", "Autumn Leaves.jps"));
        _directory.Create(Path.Combine("Musik", "Jazz", "Standards", "Latin", "x.mp3"));
        var browser = await StartAsync(MemoryWithMusic());

        await browser.OpenFolderCommand.ExecuteAsync(browser.Folders.Single());
        await browser.OpenFolderCommand.ExecuteAsync(browser.Folders.Single());
        await browser.OpenFolderCommand.ExecuteAsync(browser.Folders.Single());

        Assert.Equal(Music("Jazz", "Standards"), browser.Location);
        Assert.Equal(
            [
                new BreadcrumbItem(null, null),
                new BreadcrumbItem("Musik", MusicPath),
                new BreadcrumbItem("Jazz", Music("Jazz")),
                new BreadcrumbItem("Standards", Music("Jazz", "Standards")),
            ],
            browser.Breadcrumbs);
        Assert.Equal([new FolderItem("Latin", Music("Jazz", "Standards", "Latin"), true)], browser.Folders);
        Assert.Equal(["Autumn Leaves", "Blues in F"], browser.Pieces.Select(piece => piece.Title));
        Assert.Equal(["Autumn Leaves - Demo.mp3", "Autumn Leaves - Playalong 80.mp3"], browser.Pieces[0].Files.Select(file => file.Name));
        Assert.Equal(Music("Jazz", "Standards", "Blues in F.mp3"), browser.Pieces[1].Files[0].Path);
        Assert.True(browser.Pieces[0].HasSeveralFiles);
        Assert.False(browser.Pieces[1].HasSeveralFiles);
        Assert.False(browser.IsEmpty);
    }

    [Fact]
    public async Task JumpsBackThroughTheBreadcrumbs()
    {
        _directory.Create(Path.Combine("Musik", "Jazz", "a.mp3"));
        var browser = await StartAsync(MemoryWithMusic(Music("Jazz")));

        await browser.OpenBreadcrumbCommand.ExecuteAsync(browser.Breadcrumbs[1]);
        Assert.Equal(MusicPath, browser.Location);

        await browser.OpenBreadcrumbCommand.ExecuteAsync(browser.Breadcrumbs[0]);
        Assert.True(browser.IsTop);
        Assert.Equal([new FolderItem("Musik", MusicPath, true)], browser.Folders);
    }

    [Fact]
    public async Task StartsInTheLastFolder()
    {
        _directory.Create(Path.Combine("Musik", "Jazz", "a.mp3"));

        var browser = await StartAsync(MemoryWithMusic(Music("Jazz")));

        Assert.Equal(Music("Jazz"), browser.Location);
        Assert.Equal(["a"], browser.Pieces.Select(piece => piece.Title));
    }

    [Fact]
    public async Task StartsAtTheTopWhenTheLastFolderIsNoLongerInAMusicFolder()
    {
        var browser = await StartAsync(MemoryWithMusic(_directory.File("Anderswo")));

        Assert.True(browser.IsTop);
    }

    [Fact]
    public async Task ReportsUnreachableAndEmptyFolders()
    {
        Directory.CreateDirectory(Music("Leer"));
        var browser = await StartAsync(MemoryWithMusic(Music("Leer")));
        Assert.True(browser.IsEmpty);
        Assert.False(browser.IsUnreachable);

        Directory.Delete(Music("Leer"));
        await browser.RefreshCommand.ExecuteAsync(null);
        Assert.True(browser.IsUnreachable);
        Assert.False(browser.IsEmpty);
    }

    [Fact]
    public async Task SelectingAPieceChoosesItsFirstFile()
    {
        _directory.Create(Path.Combine("Musik", "Blues - Demo.mp3"));
        _directory.Create(Path.Combine("Musik", "Blues - Playalong.mp3"));
        var browser = await StartAsync(MemoryWithMusic(MusicPath));
        var piece = browser.Pieces.Single();

        browser.SelectPieceCommand.Execute(piece);

        Assert.Equal(new PieceFile(MusicPath, "Blues", "Blues - Demo.mp3"), browser.SelectedFile);
        Assert.True(piece.IsSelected);
        Assert.True(piece.Files[0].IsSelected);
        Assert.False(piece.Files[1].IsSelected);
    }

    [Fact]
    public async Task KeepsTheSelectionAfterRefreshingAndComingBack()
    {
        _directory.Create(Path.Combine("Musik", "Blues - Demo.mp3"));
        _directory.Create(Path.Combine("Musik", "Blues - Playalong.mp3"));
        _directory.Create(Path.Combine("Musik", "Jazz", "a.mp3"));
        var browser = await StartAsync(MemoryWithMusic(MusicPath));

        browser.SelectFileCommand.Execute(browser.Pieces.Single().Files[1]);
        await browser.RefreshCommand.ExecuteAsync(null);
        Assert.True(browser.Pieces.Single().Files[1].IsSelected);

        await browser.OpenFolderCommand.ExecuteAsync(browser.Folders.Single());
        Assert.Equal(Music("Blues - Playalong.mp3"), browser.SelectedFile?.Path);
        await browser.OpenBreadcrumbCommand.ExecuteAsync(browser.Breadcrumbs[1]);

        Assert.True(browser.Pieces.Single().IsSelected);
        Assert.False(browser.Pieces.Single().Files[0].IsSelected);
        Assert.True(browser.Pieces.Single().Files[1].IsSelected);
    }

    [Fact]
    public async Task ShowsOnlyTheFolderOpenedLast()
    {
        _directory.Create(Path.Combine("Musik", "A", "a.mp3"));
        _directory.Create(Path.Combine("Musik", "B", "b.mp3"));
        var browser = await StartAsync(MemoryWithMusic(MusicPath));
        var folders = browser.Folders;

        var first = browser.OpenFolderCommand.ExecuteAsync(folders[0]);
        var second = browser.OpenFolderCommand.ExecuteAsync(folders[1]);
        await Task.WhenAll(first, second);

        Assert.Equal(Music("B"), browser.Location);
        Assert.Equal(["b"], browser.Pieces.Select(piece => piece.Title));
        Assert.False(browser.IsLoading);
    }

    [Fact]
    public async Task RemovesAMusicFolderAndLeavesItWhenItWasOpen()
    {
        var other = _directory.File("Noten");
        Directory.CreateDirectory(other);
        _directory.Create(Path.Combine("Musik", "Jazz", "a.mp3"));
        var memory = Memory(new DeviceMemory { MusicFolders = [MusicPath, other], LastFolder = Music("Jazz") });
        var browser = await StartAsync(memory);

        await browser.RemoveMusicFolderCommand.ExecuteAsync(MusicPath + Path.DirectorySeparatorChar);

        Assert.Equal([other], browser.MusicFolders);
        Assert.Equal([other], memory.Value.MusicFolders);
        Assert.True(browser.IsTop);
        Assert.Equal([new FolderItem("Noten", other, true)], browser.Folders);
        Assert.True(Directory.Exists(MusicPath));
    }

    [Fact]
    public async Task ShowsTheHintAgainWhenTheLastMusicFolderIsRemoved()
    {
        var browser = await StartAsync(MemoryWithMusic());

        await browser.RemoveMusicFolderCommand.ExecuteAsync(MusicPath);

        Assert.Empty(browser.MusicFolders);
        Assert.True(browser.HasNoMusicFolders);
        Assert.Empty(browser.Folders);
    }

    private sealed class FakeFolderPicker : IFolderPicker
    {
        public string? Next { get; set; }

        public Task<string?> PickFolderAsync() => Task.FromResult(Next);
    }
}
