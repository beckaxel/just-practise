using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JustPractise.App.Localization;

namespace JustPractise.App.Updates;

public enum UpdateState
{
    /// <summary>Nicht über ein Paket installiert, Updates gehen nicht.</summary>
    NotInstalled,
    Idle,
    Checking,
    UpToDate,
    Available,
    Downloading,
    /// <summary>Heruntergeladen, wird beim nächsten Start installiert.</summary>
    Ready,
    NoConnection,
    Failed,
}

/// <summary>
/// Der Abschnitt „Updates“ im Einstellungsfenster: nur auf Knopfdruck suchen, der Nutzer entscheidet über das
/// Herunterladen, installiert wird beim nächsten Start oder wenn er „Jetzt neu starten“ wählt.
/// </summary>
public sealed partial class UpdatesViewModel : ObservableObject
{
    private readonly IUpdateService _updates;
    private readonly Translator _translator;

    public UpdatesViewModel(IUpdateService updates, Translator translator)
    {
        ArgumentNullException.ThrowIfNull(updates);
        ArgumentNullException.ThrowIfNull(translator);
        _updates = updates;
        _translator = translator;
        State = updates.InstalledVersion is null ? UpdateState.NotInstalled
            : updates.IsRestartPending ? UpdateState.Ready
            : UpdateState.Idle;
        translator.PropertyChanged += OnTranslatorChanged;
        UpdateTexts();
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanCheck), nameof(CanDownload), nameof(CanRestart), nameof(ShowCheck))]
    [NotifyCanExecuteChangedFor(nameof(CheckCommand), nameof(DownloadCommand), nameof(RestartCommand))]
    public partial UpdateState State { get; private set; }

    [ObservableProperty]
    public partial string? AvailableVersion { get; private set; }

    [ObservableProperty]
    public partial int Progress { get; private set; }

    [ObservableProperty]
    public partial string VersionText { get; private set; } = "";

    [ObservableProperty]
    public partial string Message { get; private set; } = "";

    public bool CanCheck => State is UpdateState.Idle or UpdateState.UpToDate or UpdateState.NoConnection or UpdateState.Failed;

    /// <summary>Der Knopf „Nach Updates suchen“ weicht „Herunterladen“ und „Jetzt neu starten“.</summary>
    public bool ShowCheck => State is not (UpdateState.NotInstalled or UpdateState.Available or UpdateState.Ready);

    public bool CanDownload => State is UpdateState.Available;

    public bool CanRestart => State is UpdateState.Ready;

    /// <summary>Der Nutzer will gleich neu starten; die App beendet sich auf dem normalen Weg und speichert dabei.</summary>
    public event EventHandler? RestartRequested;

    [RelayCommand(CanExecute = nameof(CanCheck))]
    private Task CheckAsync() => RunAsync(UpdateState.Checking, async () =>
    {
        AvailableVersion = await _updates.CheckAsync(CancellationToken.None);
        return AvailableVersion is null ? UpdateState.UpToDate : UpdateState.Available;
    });

    [RelayCommand(CanExecute = nameof(CanDownload))]
    private Task DownloadAsync()
    {
        Progress = 0;

        // Progress<T> meldet im Thread, in dem es angelegt wurde, also in der Oberfläche.
        var progress = new Progress<int>(percent =>
        {
            Progress = percent;
            UpdateTexts();
        });
        return RunAsync(UpdateState.Downloading, async () =>
        {
            await _updates.DownloadAsync(progress, CancellationToken.None);
            return UpdateState.Ready;
        });
    }

    [RelayCommand(CanExecute = nameof(CanRestart))]
    private void Restart()
    {
        _updates.ApplyAfterExit();
        RestartRequested?.Invoke(this, EventArgs.Empty);
    }

    private async Task RunAsync(UpdateState running, Func<Task<UpdateState>> step)
    {
        State = running;
        UpdateTexts();
        try
        {
            State = await step();
        }
#pragma warning disable CA1031 // Jeder Fehler beim Suchen oder Herunterladen wird nur gemeldet, die App läuft weiter.
        catch (Exception exception)
#pragma warning restore CA1031
        {
            // Ohne Statuscode kam keine Antwort an: keine Verbindung. Mit Statuscode hat der Server abgelehnt.
            State = exception is HttpRequestException { StatusCode: null } or TimeoutException or IOException
                ? UpdateState.NoConnection
                : UpdateState.Failed;
        }

        UpdateTexts();
    }

    private void OnTranslatorChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(Translator.Language))
        {
            UpdateTexts();
        }
    }

    private void UpdateTexts()
    {
        var installed = _updates.InstalledVersion;
        VersionText = installed is null
            ? _translator.Get("updates.notInstalled")
            : _translator.Format("updates.version", new Dictionary<string, object?> { ["version"] = installed });
        Message = State switch
        {
            UpdateState.Checking => _translator.Get("updates.checking"),
            UpdateState.UpToDate => _translator.Get("updates.upToDate"),
            UpdateState.Available => _translator.Format("updates.available", new Dictionary<string, object?> { ["version"] = AvailableVersion }),
            UpdateState.Downloading => _translator.Format("updates.downloading", new Dictionary<string, object?> { ["percent"] = Progress }),
            UpdateState.Ready => _translator.Get("updates.ready"),
            UpdateState.NoConnection => _translator.Get("updates.noConnection"),
            UpdateState.Failed => _translator.Get("updates.failed"),
            _ => "",
        };
    }
}
