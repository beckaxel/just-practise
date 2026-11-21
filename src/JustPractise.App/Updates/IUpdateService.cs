namespace JustPractise.App.Updates;

/// <summary>
/// Updates aus den GitHub-Releases, nur auf Knopfdruck. Ein heruntergeladenes Update wird beim nächsten Start
/// installiert, nie mitten im Üben.
/// </summary>
public interface IUpdateService
{
    /// <summary>Die installierte Version; <c>null</c>, wenn die App nicht über ein Paket installiert ist (z. B. <c>dotnet run</c>).</summary>
    string? InstalledVersion { get; }

    /// <summary>Die Seite mit allen Versionen, für die portable Variante, die sich nicht selbst aktualisiert.</summary>
    Uri ReleasesPage { get; }

    /// <summary>Ein Update ist heruntergeladen und wird beim nächsten Start installiert.</summary>
    bool IsRestartPending { get; }

    /// <summary>Fragt die neueste Version ab; <c>null</c>, wenn die installierte aktuell ist.</summary>
    /// <exception cref="HttpRequestException">Keine Verbindung oder der Server antwortet mit einem Fehler.</exception>
    Task<string?> CheckAsync(CancellationToken cancellationToken);

    /// <summary>Lädt das zuletzt gefundene Update herunter und meldet den Fortschritt in Prozent.</summary>
    Task DownloadAsync(IProgress<int> progress, CancellationToken cancellationToken);

    /// <summary>Installiert das heruntergeladene Update, sobald die App beendet ist, und startet sie danach neu.</summary>
    void ApplyAfterExit();
}
