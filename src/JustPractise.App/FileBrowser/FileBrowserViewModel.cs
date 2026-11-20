using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JustPractise.Core.Settings;
using JustPractise.Storage;

namespace JustPractise.App.FileBrowser;

/// <summary>
/// Der Dateibrowser: zeigt immer einen Ordner mit Pfadzeile, Unterordnern und Stücken. Auf der obersten Ebene
/// stehen die Musikordner. Der zuletzt geöffnete Ordner wird im gerätespezifischen Gedächtnis gemerkt.
/// </summary>
/// <remarks>
/// Ordner werden im Hintergrund gelesen, weil Cloud-Ordner blockieren können. Wer währenddessen weiterklickt,
/// bekommt nur das Ergebnis des zuletzt gewählten Ordners zu sehen. Aufrufe kommen vom UI-Thread.
/// </remarks>
public sealed partial class FileBrowserViewModel(DataFileKeeper<DeviceMemory> memory, IFolderPicker picker) : ObservableObject
{
    private int _version;

    /// <summary>Der angezeigte Ordner oder <c>null</c> für die oberste Ebene.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsTop))]
    public partial string? Location { get; private set; }

    public bool IsTop => Location is null;

    [ObservableProperty]
    public partial IReadOnlyList<BreadcrumbItem> Breadcrumbs { get; private set; } = [new(null, null)];

    [ObservableProperty]
    public partial IReadOnlyList<FolderItem> Folders { get; private set; } = [];

    [ObservableProperty]
    public partial IReadOnlyList<PieceItem> Pieces { get; private set; } = [];

    [ObservableProperty]
    public partial bool IsLoading { get; private set; }

    /// <summary>Oberste Ebene ohne Musikordner, z. B. beim ersten Start.</summary>
    [ObservableProperty]
    public partial bool HasNoMusicFolders { get; private set; }

    [ObservableProperty]
    public partial bool IsUnreachable { get; private set; }

    /// <summary>Ein erreichbarer Ordner ohne Unterordner und Stücke.</summary>
    [ObservableProperty]
    public partial bool IsEmpty { get; private set; }

    /// <summary>Die gewählte Audiodatei als absoluter Pfad.</summary>
    [ObservableProperty]
    public partial string? SelectedFile { get; private set; }

    /// <summary>Öffnet den zuletzt geöffneten Ordner, wenn er noch in einem Musikordner liegt, sonst die oberste Ebene.</summary>
    public Task StartAsync() => ShowAsync(memory.Value.LastFolder, keepContent: false);

    [RelayCommand(AllowConcurrentExecutions = true)]
    private Task OpenFolder(FolderItem folder) => ShowAsync(folder.Path, keepContent: false);

    [RelayCommand(AllowConcurrentExecutions = true)]
    private Task OpenBreadcrumb(BreadcrumbItem crumb) => ShowAsync(crumb.Path, keepContent: false);

    [RelayCommand(AllowConcurrentExecutions = true)]
    private Task Refresh() => ShowAsync(Location, keepContent: true);

    /// <summary>Fügt einen Musikordner über den Dialog des Systems hinzu und öffnet ihn.</summary>
    [RelayCommand]
    private async Task AddMusicFolder()
    {
        var picked = await picker.PickFolderAsync();
        if (picked is null)
        {
            return;
        }

        var path = FolderPaths.Normalize(picked);
        memory.Update(value => value.MusicFolders.Any(folder => FolderPaths.AreSame(folder, path))
            ? value
            : value with { MusicFolders = [.. value.MusicFolders, path] });
        await ShowAsync(path, keepContent: false);
    }

    /// <summary>Ein Klick auf ein Stück wählt seine erste Datei.</summary>
    [RelayCommand]
    private void SelectPiece(PieceItem piece) => Select(piece.Files[0].Path);

    [RelayCommand]
    private void SelectFile(FileItem file) => Select(file.Path);

    private void Select(string path)
    {
        SelectedFile = path;
        MarkSelection();
    }

    private async Task ShowAsync(string? path, bool keepContent)
    {
        var version = ++_version;
        var roots = memory.Value.MusicFolders;
        if (path is not null)
        {
            // Liegt der Ordner nicht (mehr) in einem Musikordner, geht es auf die oberste Ebene.
            path = FolderPaths.FindRoot(roots, path) is null ? null : FolderPaths.Normalize(path);
        }

        Location = path;
        Breadcrumbs = BuildBreadcrumbs(roots, path);
        IsLoading = true;
        if (!keepContent)
        {
            Show([], [], unreachable: false, empty: false);
        }

        memory.Update(value => value.LastFolder == path ? value : value with { LastFolder = path });

        if (path is null)
        {
            var folders = await Task.Run(() => roots
                .Select(root => new FolderItem(FolderPaths.DisplayName(root), root, Directory.Exists(root)))
                .ToList());
            if (version == _version)
            {
                Show(folders, [], unreachable: false, empty: false);
            }
        }
        else
        {
            var content = await Task.Run(() => FolderReader.Read(path));
            if (version == _version)
            {
                var folders = content.Subfolders.Select(name => new FolderItem(name, Path.Combine(path, name), true)).ToList();
                var pieces = content.Pieces
                    .Select(piece => new PieceItem(piece.Title, [.. piece.Files.Select(file => new FileItem(file, Path.Combine(path, file)))]))
                    .ToList();
                Show(folders, pieces, unreachable: !content.IsReachable, empty: content.IsReachable && folders.Count == 0 && pieces.Count == 0);
            }
        }

        if (version == _version)
        {
            IsLoading = false;
        }
    }

    private void Show(IReadOnlyList<FolderItem> folders, IReadOnlyList<PieceItem> pieces, bool unreachable, bool empty)
    {
        Folders = folders;
        Pieces = pieces;
        IsUnreachable = unreachable;
        IsEmpty = empty;
        HasNoMusicFolders = Location is null && memory.Value.MusicFolders.Count == 0;
        MarkSelection();
    }

    private void MarkSelection()
    {
        foreach (var piece in Pieces)
        {
            var selected = false;
            foreach (var file in piece.Files)
            {
                file.IsSelected = SelectedFile is not null && string.Equals(file.Path, SelectedFile, FolderPaths.Comparison);
                selected |= file.IsSelected;
            }

            piece.IsSelected = selected;
        }
    }

    private static List<BreadcrumbItem> BuildBreadcrumbs(IReadOnlyList<string> roots, string? path)
    {
        var crumbs = new List<BreadcrumbItem> { new(null, null) };
        if (path is not null && FolderPaths.FindRoot(roots, path) is { } root)
        {
            var current = FolderPaths.Normalize(root);
            crumbs.Add(new BreadcrumbItem(FolderPaths.DisplayName(root), current));
            foreach (var segment in FolderPaths.Segments(root, path))
            {
                current = Path.Combine(current, segment);
                crumbs.Add(new BreadcrumbItem(segment, current));
            }
        }

        return crumbs;
    }
}
