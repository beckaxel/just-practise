namespace JustPractise.App.FileBrowser;

/// <summary>Der Dialog des Systems zur Auswahl eines Ordners.</summary>
public interface IFolderPicker
{
    /// <summary>Der gewählte Ordner als absoluter Pfad oder <c>null</c>, wenn abgebrochen wurde.</summary>
    Task<string?> PickFolderAsync();
}
