using System.Reflection;
using Velopack;
using Velopack.Sources;

namespace JustPractise.App.Updates;

/// <summary>Updates über Velopack aus den GitHub-Releases des Projekts.</summary>
/// <remarks>
/// Vorabversionen (z. B. <c>0.1.0-rc.1</c>) bekommen auch Vorabversionen angeboten, alle anderen nur fertige Versionen.
/// Ohne Zugangsschlüssel erlaubt GitHub 60 Abfragen pro Stunde und IP-Adresse; bei Abfragen nur auf Knopfdruck reicht das.
/// </remarks>
public sealed class VelopackUpdateService : IUpdateService
{
    public const string RepositoryUrl = "https://github.com/beckaxel/just-practise";

    private readonly UpdateManager _manager = new(new GithubSource(RepositoryUrl, accessToken: null, prerelease: IsPrerelease()));
    private UpdateInfo? _found;

    public string? InstalledVersion => _manager.IsInstalled ? _manager.CurrentVersion?.ToString() : null;

    public bool IsRestartPending => _manager.IsInstalled && _manager.UpdatePendingRestart is not null;

    public async Task<string?> CheckAsync(CancellationToken cancellationToken)
    {
        _found = await _manager.CheckForUpdatesAsync().WaitAsync(cancellationToken).ConfigureAwait(false);
        return _found?.TargetFullRelease.Version.ToString();
    }

    public Task DownloadAsync(IProgress<int> progress, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(progress);
        var found = _found ?? throw new InvalidOperationException("No update found yet.");
        return _manager.DownloadUpdatesAsync(found, progress.Report, cancellationToken);
    }

    public void ApplyAfterExit()
    {
        if (_manager.UpdatePendingRestart is { } pending)
        {
            _manager.WaitExitThenApplyUpdates(pending, silent: false, restart: true);
        }
    }

    private static bool IsPrerelease() =>
        typeof(VelopackUpdateService).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            .Split('+')[0].Contains('-', StringComparison.Ordinal) ?? false;
}
