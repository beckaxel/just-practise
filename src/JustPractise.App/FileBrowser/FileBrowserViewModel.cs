using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JustPractise.Core.Settings;
using JustPractise.Storage;

namespace JustPractise.App.FileBrowser;

/// <summary>
/// Die Bibliothek: ein Baum aus den Musikordnern mit ihren Unterordnern und Stücken, die sich an Ort und Stelle
/// aufklappen lassen. Eine Pfadzeile zeigt, wo die gewählte Stelle liegt. Was aufgeklappt und gewählt war, merkt sich
/// das gerätespezifische Gedächtnis; die zuletzt gewählte Datei wird beim Start wieder geladen.
/// </summary>
/// <remarks>
/// Ordner werden im Hintergrund gelesen, weil Cloud-Ordner blockieren können. Wird der Baum währenddessen neu
/// aufgebaut, verfällt das ältere Ergebnis. Aufrufe kommen vom UI-Thread.
/// </remarks>
public sealed partial class FileBrowserViewModel(DataFileKeeper<DeviceMemory> memory, IFolderPicker picker, IRemoveFolderDialog removeDialog)
    : ObservableObject
{
    private static readonly StringComparer PathComparer =
        FolderPaths.Comparison == StringComparison.Ordinal ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase;

    private readonly HashSet<string> _expandedFolders = new(PathComparer);
    private readonly HashSet<string> _expandedPieces = new(PathComparer);
    private int _version;

    /// <summary>Die Zeilen des Baums in Anzeigereihenfolge.</summary>
    public ObservableCollection<LibraryRow> Rows { get; } = [];

    /// <summary>Die gewählte Stelle (ein Ordner) für die Pfadzeile, <c>null</c> ohne Auswahl.</summary>
    [ObservableProperty]
    public partial string? Location { get; private set; }

    [ObservableProperty]
    public partial IReadOnlyList<BreadcrumbItem> Breadcrumbs { get; private set; } = [];

    [ObservableProperty]
    public partial bool IsLoading { get; private set; }

    /// <summary>Noch kein Musikordner gewählt, z. B. beim ersten Start.</summary>
    [ObservableProperty]
    public partial bool HasNoMusicFolders { get; private set; }

    /// <summary>Die gewählte Audiodatei; der Player lädt sie.</summary>
    [ObservableProperty]
    public partial PieceFile? SelectedFile { get; private set; }

    /// <summary>Die Ansicht soll diese Zeile sichtbar machen, z. B. die gewählte Datei nach dem Start.</summary>
    public event EventHandler<LibraryRow>? ScrollRequested;

    /// <summary>
    /// Gibt die gewählte Datei frei, bevor ihr Musikordner entfernt wird (der Player schließt sie und schreibt
    /// ausstehende Änderungen), damit danach nichts mehr in den Ordner schreibt.
    /// </summary>
    public Func<Task>? ReleaseSelection { get; set; }

    /// <summary>Die gewählten Musikordner.</summary>
    public IReadOnlyList<string> MusicFolders => memory.Value.MusicFolders;

    public bool HasMusicFolders => MusicFolders.Count > 0;

    /// <summary>
    /// Baut den Baum wie beim letzten Beenden auf und wählt die zuletzt gewählte Datei wieder, wenn es sie noch gibt.
    /// </summary>
    public async Task StartAsync()
    {
        var saved = memory.Value;
        _expandedFolders.UnionWith(saved.Library.ExpandedFolders);
        _expandedPieces.UnionWith(saved.Library.ExpandedPieces);
        if (saved.Library.IsDefault && saved.LastFolder is { } last)
        {
            // Ältere Versionen merkten sich nur den zuletzt geöffneten Ordner: den Weg dorthin aufklappen.
            ExpandPathTo(last);
        }

        SetLocation(saved.LastFolder);
        await RebuildAsync();

        if (saved.Library.SelectedFile is { } path && FolderPaths.FindRoot(MusicFolders, path) is not null && File.Exists(path))
        {
            var row = Rows.FirstOrDefault(candidate => candidate.IsFile && SamePath(candidate.Key, path));
            if (row is not null)
            {
                SetLocation(row.Folder);
                Select(row);
                ScrollRequested?.Invoke(this, row);
            }
        }
    }

    /// <summary>
    /// Ein Tipp auf eine Zeile: Ordner und virtuelle Stücke klappen auf oder zu, eine Datei wird gewählt und geladen.
    /// </summary>
    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task Activate(LibraryRow row)
    {
        SetLocation(row.Folder);
        switch (row.Kind)
        {
            case LibraryRowKind.File:
                Select(row);
                break;
            case LibraryRowKind.Piece:
                TogglePiece(row);
                MarkSelection();
                break;
            case LibraryRowKind.Folder:
                await ToggleFolderAsync(row);
                MarkSelection();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(row), row.Kind, null);
        }

        SaveState();
    }

    /// <summary>Ein Glied der Pfadzeile: Der Ordner wird aufgeklappt und sichtbar gemacht.</summary>
    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task OpenBreadcrumb(BreadcrumbItem crumb)
    {
        SetLocation(crumb.Path);
        ExpandPathTo(crumb.Path);
        SaveState();
        await RebuildAsync();
        if (Rows.FirstOrDefault(row => row.IsFolder && SamePath(row.Key, crumb.Path)) is { } target)
        {
            ScrollRequested?.Invoke(this, target);
        }
    }

    [RelayCommand(AllowConcurrentExecutions = true)]
    private Task Refresh() => RebuildAsync();

    /// <summary>Fügt einen Musikordner über den Dialog des Systems hinzu und klappt ihn auf.</summary>
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
        OnPropertyChanged(nameof(MusicFolders));
        OnPropertyChanged(nameof(HasMusicFolders));

        _expandedFolders.Add(path);
        SetLocation(path);
        SaveState();
        await RebuildAsync();
        if (Rows.FirstOrDefault(row => row.IsFolder && SamePath(row.Key, path)) is { } added)
        {
            ScrollRequested?.Invoke(this, added);
        }
    }

    /// <summary>
    /// Nimmt einen Musikordner nach Rückfrage aus der Bibliothek; die Musikdateien bleiben, wo sie sind. Auf Wunsch
    /// werden die Sidecar-Dateien darin gelöscht, außer in anderen Musikordnern, die darin liegen.
    /// </summary>
    [RelayCommand]
    private async Task RemoveMusicFolder(string path)
    {
        if (MusicFolders.FirstOrDefault(folder => FolderPaths.AreSame(folder, path)) is not { } root)
        {
            return;
        }

        var others = MusicFolders.Where(folder => !FolderPaths.AreSame(folder, root)).ToList();
        IReadOnlyList<string> sidecars = await Task.Run(() => Directory.Exists(root) ? SidecarCleanup.Find(root, others) : []);
        var choice = await removeDialog.AskAsync(FolderPaths.DisplayName(root), sidecars.Count);
        if (choice == RemoveFolderChoice.Cancel)
        {
            return;
        }

        if (SelectedFile is { } selected && FolderPaths.FindRoot([root], selected.Path) is not null)
        {
            if (ReleaseSelection is { } release)
            {
                await release();
            }

            SelectedFile = null;
        }

        memory.Update(value => value with
        {
            MusicFolders = [.. value.MusicFolders.Where(folder => !FolderPaths.AreSame(folder, root))],
        });
        OnPropertyChanged(nameof(MusicFolders));
        OnPropertyChanged(nameof(HasMusicFolders));

        _expandedFolders.RemoveWhere(folder => FolderPaths.FindRoot([root], folder) is not null && FolderPaths.FindRoot(MusicFolders, folder) is null);
        _expandedPieces.RemoveWhere(piece => FolderPaths.FindRoot([root], piece) is not null && FolderPaths.FindRoot(MusicFolders, piece) is null);
        if (Location is not null && FolderPaths.FindRoot(MusicFolders, Location) is null)
        {
            SetLocation(null);
        }

        SaveState();
        await RebuildAsync();

        if (choice == RemoveFolderChoice.RemoveWithSidecars)
        {
            await Task.Run(() => SidecarCleanup.Delete(sidecars));
        }
    }

    private void Select(LibraryRow row)
    {
        SelectedFile = row.File;
        MarkSelection();
    }

    private void TogglePiece(LibraryRow row)
    {
        if (row.IsExpanded)
        {
            _expandedPieces.Remove(row.Key);
            Collapse(row);
            return;
        }

        _expandedPieces.Add(row.Key);
        row.IsExpanded = true;
        Insert(row, PieceFileRows(row.Files, row.Depth + 1));
    }

    private async Task ToggleFolderAsync(LibraryRow row)
    {
        if (!row.HasChildren)
        {
            return;
        }

        if (row.IsExpanded)
        {
            _expandedFolders.Remove(row.Key);
            Collapse(row);
            return;
        }

        _expandedFolders.Add(row.Key);
        row.IsExpanded = true;
        row.IsLoading = true;
        var version = _version;
        var children = await ReadChildrenAsync(row.Key, row.Depth + 1);
        row.IsLoading = false;
        if (version != _version || !row.IsExpanded || !Rows.Contains(row))
        {
            return; // Inzwischen neu aufgebaut oder wieder zugeklappt.
        }

        row.HasChildren = children.Count > 0;
        row.IsExpanded = children.Count > 0;
        Insert(row, children);
    }

    private void Collapse(LibraryRow row)
    {
        row.IsExpanded = false;
        var index = Rows.IndexOf(row);
        while (index + 1 < Rows.Count && Rows[index + 1].Depth > row.Depth)
        {
            Rows.RemoveAt(index + 1);
        }
    }

    private void Insert(LibraryRow parent, IReadOnlyList<LibraryRow> children)
    {
        var index = Rows.IndexOf(parent);
        for (var i = 0; i < children.Count; i++)
        {
            Rows.Insert(index + 1 + i, children[i]);
        }
    }

    /// <summary>Baut den ganzen Baum neu auf; aufgeklappt bleibt, was aufgeklappt war.</summary>
    private async Task RebuildAsync()
    {
        var version = ++_version;
        IsLoading = true;
        var roots = MusicFolders;
        var rows = new List<LibraryRow>();
        foreach (var root in roots)
        {
            var path = FolderPaths.Normalize(root);
            var (reachable, hasChildren) = await Task.Run(() => (Directory.Exists(path), FolderReader.HasEntries(path)));
            var row = LibraryRow.ForFolder(path, FolderPaths.DisplayName(path), 0, hasChildren, reachable);
            rows.Add(row);
            if (row.HasChildren && _expandedFolders.Contains(path))
            {
                var children = await ReadChildrenAsync(path, 1);
                row.IsExpanded = children.Count > 0;
                rows.AddRange(children);
            }
        }

        if (version != _version)
        {
            return;
        }

        Rows.Clear();
        foreach (var row in rows)
        {
            Rows.Add(row);
        }

        HasNoMusicFolders = roots.Count == 0;
        MarkSelection();
        IsLoading = false;
    }

    /// <summary>Liest einen Ordner und darunter alles, was aufgeklappt war.</summary>
    private async Task<IReadOnlyList<LibraryRow>> ReadChildrenAsync(string folder, int depth)
    {
        var (content, subfolders) = await Task.Run(() =>
        {
            var read = FolderReader.Read(folder);
            var folders = read.Subfolders
                .Select(name => (Name: name, Path: Path.Combine(folder, name)))
                .Select(sub => (sub.Name, sub.Path, HasChildren: FolderReader.HasEntries(sub.Path)))
                .ToList();
            return (read, folders);
        });

        var rows = new List<LibraryRow>();
        foreach (var (name, path, hasChildren) in subfolders)
        {
            var row = LibraryRow.ForFolder(path, name, depth, hasChildren);
            rows.Add(row);
            if (hasChildren && _expandedFolders.Contains(path))
            {
                var children = await ReadChildrenAsync(path, depth + 1);
                row.IsExpanded = children.Count > 0;
                rows.AddRange(children);
            }
        }

        foreach (var piece in content.Pieces)
        {
            var files = piece.Files.Select(file => new PieceFile(folder, piece.Title, file)).ToList();
            if (files.Count == 1)
            {
                rows.Add(LibraryRow.ForFile(files[0], piece.Title, depth));
                continue;
            }

            var row = LibraryRow.ForPiece(folder, piece.Title, files, depth);
            rows.Add(row);
            if (_expandedPieces.Contains(row.Key))
            {
                row.IsExpanded = true;
                rows.AddRange(PieceFileRows(files, depth + 1));
            }
        }

        return rows;
    }

    private static List<LibraryRow> PieceFileRows(IReadOnlyList<PieceFile> files, int depth) =>
        [.. files.Select(file => LibraryRow.ForFile(file, file.FileName, depth))];

    private void MarkSelection()
    {
        var selected = SelectedFile?.Path;
        foreach (var row in Rows)
        {
            row.IsSelected = selected is not null && row.Kind switch
            {
                LibraryRowKind.File => SamePath(row.Key, selected),
                LibraryRowKind.Piece => !row.IsExpanded && row.Files.Any(file => SamePath(file.Path, selected)),
                _ => false,
            };
        }
    }

    /// <summary>Klappt den Musikordner und alle Ordner bis einschließlich <paramref name="path"/> auf.</summary>
    private void ExpandPathTo(string path)
    {
        if (FolderPaths.FindRoot(MusicFolders, path) is not { } root)
        {
            return;
        }

        var current = FolderPaths.Normalize(root);
        _expandedFolders.Add(current);
        foreach (var segment in FolderPaths.Segments(root, path))
        {
            current = Path.Combine(current, segment);
            _expandedFolders.Add(current);
        }
    }

    private void SetLocation(string? path)
    {
        if (path is not null && FolderPaths.FindRoot(MusicFolders, path) is null)
        {
            path = null;
        }

        Location = path is null ? null : FolderPaths.Normalize(path);
        Breadcrumbs = BuildBreadcrumbs(MusicFolders, Location);
    }

    private void SaveState()
    {
        var library = new LibraryState
        {
            ExpandedFolders = [.. _expandedFolders.Order(StringComparer.Ordinal)],
            ExpandedPieces = [.. _expandedPieces.Order(StringComparer.Ordinal)],
            SelectedFile = SelectedFile?.Path,
        };
        memory.Update(value =>
        {
            var changed = value with { LastFolder = Location, Library = library with { Unknown = value.Library.Unknown } };
            return changed.LastFolder == value.LastFolder
                && changed.Library.SelectedFile == value.Library.SelectedFile
                && changed.Library.ExpandedFolders.SequenceEqual(value.Library.ExpandedFolders)
                && changed.Library.ExpandedPieces.SequenceEqual(value.Library.ExpandedPieces)
                ? value
                : changed;
        });
    }

    private static bool SamePath(string a, string b) => string.Equals(a, b, FolderPaths.Comparison);

    private static List<BreadcrumbItem> BuildBreadcrumbs(IReadOnlyList<string> roots, string? path)
    {
        var crumbs = new List<BreadcrumbItem>();
        if (path is not null && FolderPaths.FindRoot(roots, path) is { } root)
        {
            var current = FolderPaths.Normalize(root);
            crumbs.Add(new BreadcrumbItem(FolderPaths.DisplayName(root), current) { IsFirst = true });
            foreach (var segment in FolderPaths.Segments(root, path))
            {
                current = Path.Combine(current, segment);
                crumbs.Add(new BreadcrumbItem(segment, current));
            }
        }

        return crumbs;
    }
}
