using CommunityToolkit.Mvvm.ComponentModel;

namespace JustPractise.App.FileBrowser;

/// <summary>Ein Glied der Pfadzeile: ein Ordner zwischen Musikordner und der gewählten Stelle.</summary>
public sealed record BreadcrumbItem(string Name, string Path)
{
    /// <summary>Das erste Glied, der Musikordner; davor steht kein Trenner.</summary>
    public bool IsFirst { get; init; }
}

/// <summary>Eine Audiodatei mit dem Stück, zu dem sie gehört; das Stück bestimmt den Namen der Sidecar-Datei.</summary>
public sealed record PieceFile(string Folder, string PieceTitle, string FileName)
{
    public string Path => System.IO.Path.Combine(Folder, FileName);
}

/// <summary>Was eine Zeile der Bibliothek zeigt.</summary>
public enum LibraryRowKind
{
    /// <summary>Ein Ordner; auf der obersten Ebene ein Musikordner.</summary>
    Folder,

    /// <summary>
    /// Ein „virtuelles“ Stück, das mehrere Audiodateien zusammenfasst. Es lässt sich aufklappen, aber nicht abspielen.
    /// </summary>
    Piece,

    /// <summary>
    /// Ein „echtes“ Stück, also eine Audiodatei, die sich abspielen lässt: ein Stück mit nur einer Datei oder eine
    /// Datei unter einem virtuellen Stück.
    /// </summary>
    File,
}

/// <summary>
/// Eine Zeile im Bibliotheksbaum. Der Baum ist eine flache Liste; die Tiefe bestimmt die Einrückung, aufgeklappte
/// Einträge stehen direkt unter ihrer Zeile.
/// </summary>
public sealed partial class LibraryRow : ObservableObject
{
    /// <summary>Einrückung pro Ebene in geräteunabhängigen Einheiten: wenig, aber deutlich erkennbar.</summary>
    public const double IndentPerLevel = 18;

    private LibraryRow(LibraryRowKind kind, string name, string key, int depth)
    {
        Kind = kind;
        Name = name;
        Key = key;
        Depth = depth;
    }

    public LibraryRowKind Kind { get; }

    /// <summary>Der angezeigte Name: Ordnername, Titel des Stücks oder Dateiname mit Endung.</summary>
    public string Name { get; }

    /// <summary>
    /// Eindeutig im Baum: Ordner- bzw. Dateipfad; für virtuelle Stücke Ordner und Titel als Pfad zusammengesetzt.
    /// </summary>
    public string Key { get; }

    public int Depth { get; }

    public double Indent => Depth * IndentPerLevel;

    public bool IsFolder => Kind == LibraryRowKind.Folder;

    public bool IsPiece => Kind == LibraryRowKind.Piece;

    public bool IsFile => Kind == LibraryRowKind.File;

    /// <summary>Ein Musikordner auf der obersten Ebene; nur er lässt sich aus der Bibliothek entfernen.</summary>
    public bool IsMusicFolder => IsFolder && Depth == 0;

    /// <summary>Der Ordner, in dem der Eintrag liegt; bei Ordnern der Ordner selbst.</summary>
    public required string Folder { get; init; }

    /// <summary>Die Audiodatei einer abspielbaren Zeile.</summary>
    public PieceFile? File { get; private init; }

    /// <summary>Die Dateien eines virtuellen Stücks.</summary>
    public IReadOnlyList<PieceFile> Files { get; private init; } = [];

    /// <summary>Nur bei Musikordnern: <c>false</c>, wenn er fehlt, z. B. eine nicht angeschlossene Platte.</summary>
    public bool IsReachable { get; private init; } = true;

    /// <summary>Ob es etwas zum Aufklappen gibt; nur dann steht ein Chevron davor.</summary>
    [ObservableProperty]
    public partial bool HasChildren { get; set; }

    [ObservableProperty]
    public partial bool IsExpanded { get; set; }

    /// <summary>Ein Ordner wird gerade gelesen.</summary>
    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    /// <summary>Die gewählte Datei, bzw. bei einem virtuellen Stück: eine seiner Dateien ist gewählt.</summary>
    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    public static LibraryRow ForFolder(string path, string name, int depth, bool hasChildren, bool isReachable = true) =>
        new(LibraryRowKind.Folder, name, path, depth)
        {
            Folder = path,
            HasChildren = hasChildren && isReachable,
            IsReachable = isReachable,
        };

    public static LibraryRow ForPiece(string folder, string title, IReadOnlyList<PieceFile> files, int depth) =>
        new(LibraryRowKind.Piece, title, System.IO.Path.Combine(folder, title), depth)
        {
            Folder = folder,
            Files = files,
            HasChildren = true,
        };

    /// <summary>Eine abspielbare Datei; ein Stück mit nur einer Datei zeigt seinen Titel, sonst den Dateinamen.</summary>
    public static LibraryRow ForFile(PieceFile file, string name, int depth) =>
        new(LibraryRowKind.File, name, file.Path, depth)
        {
            Folder = file.Folder,
            File = file,
        };
}
