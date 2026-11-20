using CommunityToolkit.Mvvm.ComponentModel;

namespace JustPractise.App.FileBrowser;

/// <summary>Ein Glied der Pfadzeile. <see cref="Path"/> ist <c>null</c> für die oberste Ebene mit den Musikordnern.</summary>
public sealed record BreadcrumbItem(string? Name, string? Path)
{
    public bool IsTop => Path is null;
}

/// <summary>Ein Ordner im Browser. Auf der obersten Ebene ein Musikordner, der auch fehlen kann.</summary>
public sealed record FolderItem(string Name, string Path, bool IsReachable);

/// <summary>Eine Audiodatei mit dem Stück, zu dem sie gehört; das Stück bestimmt den Namen der Sidecar-Datei.</summary>
public sealed record PieceFile(string Folder, string PieceTitle, string FileName)
{
    public string Path => System.IO.Path.Combine(Folder, FileName);
}

/// <summary>Eine Audiodatei eines Stücks im Browser, angezeigt mit Endung.</summary>
public sealed partial class FileItem(PieceFile file) : ObservableObject
{
    public PieceFile File { get; } = file;

    public string Name => File.FileName;

    public string Path => File.Path;

    [ObservableProperty]
    public partial bool IsSelected { get; set; }
}

/// <summary>Ein Stück im Browser. Mit mehreren Dateien lässt es sich aufklappen.</summary>
public sealed partial class PieceItem(string title, IReadOnlyList<FileItem> files) : ObservableObject
{
    public string Title { get; } = title;

    public IReadOnlyList<FileItem> Files { get; } = files;

    public bool HasSeveralFiles => Files.Count > 1;

    [ObservableProperty]
    public partial bool IsExpanded { get; set; }

    /// <summary>Gilt, wenn eine seiner Dateien ausgewählt ist.</summary>
    [ObservableProperty]
    public partial bool IsSelected { get; set; }
}
