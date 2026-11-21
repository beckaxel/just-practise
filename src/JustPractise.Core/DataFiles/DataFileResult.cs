namespace JustPractise.Core.DataFiles;

public enum DataFileStatus
{
    /// <summary>gelesen, darf überschrieben werden</summary>
    Ok,

    /// <summary>von einer neueren App-Version geschrieben: gelesen, soweit möglich, aber nicht überschreiben</summary>
    Newer,

    /// <summary>nicht lesbar: Standardwerte verwenden, Hinweis zeigen, nicht überschreiben</summary>
    Corrupt,
}

/// <summary>Der Inhalt einer gelesenen Datendatei und ob sie überschrieben werden darf.</summary>
public sealed record DataFileResult<T>(T Value, DataFileStatus Status)
{
    public bool CanOverwrite => Status == DataFileStatus.Ok;
}
