namespace JustPractise.App.FileBrowser;

/// <summary>Was beim Entfernen eines Musikordners geschehen soll.</summary>
public enum RemoveFolderChoice
{
    Cancel,

    /// <summary>Nur aus der Liste nehmen; alle Dateien bleiben.</summary>
    Remove,

    /// <summary>Aus der Liste nehmen und die Sidecar-Dateien darin löschen.</summary>
    RemoveWithSidecars,
}

/// <summary>Die Rückfrage vor dem Entfernen eines Musikordners (Grenze zur Oberfläche).</summary>
public interface IRemoveFolderDialog
{
    /// <param name="folderName">der angezeigte Name des Musikordners</param>
    /// <param name="sidecarCount">wie viele Sidecar-Dateien darin liegen; bei 0 gibt es nichts mitzulöschen</param>
    Task<RemoveFolderChoice> AskAsync(string folderName, int sidecarCount);
}
