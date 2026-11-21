using JustPractise.App.Updates;

namespace JustPractise.App.Tests.Updates;

internal sealed class FakeUpdateService : IUpdateService
{
    public string? InstalledVersion { get; set; } = "0.1.0";

    public Uri ReleasesPage { get; } = new("https://example.org/releases");

    public bool IsRestartPending { get; set; }

    /// <summary>Was die nächste Suche liefert; eine Ausnahme wird geworfen.</summary>
    public object? NextCheck { get; set; }

    public Exception? DownloadFailure { get; set; }

    public bool AppliedAfterExit { get; private set; }

    public Task<string?> CheckAsync(CancellationToken cancellationToken) =>
        NextCheck is Exception exception ? Task.FromException<string?>(exception) : Task.FromResult((string?)NextCheck);

    public Task DownloadAsync(IProgress<int> progress, CancellationToken cancellationToken)
    {
        if (DownloadFailure is { } failure)
        {
            return Task.FromException(failure);
        }

        IsRestartPending = true;
        return Task.CompletedTask;
    }

    public void ApplyAfterExit() => AppliedAfterExit = true;
}
