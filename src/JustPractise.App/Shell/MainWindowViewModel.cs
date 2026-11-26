using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JustPractise.App.FileBrowser;
using JustPractise.App.Metronome;
using JustPractise.App.Player;
using JustPractise.App.Shortcuts;
using JustPractise.App.Timer;

namespace JustPractise.App.Shell;

/// <summary>
/// Hält die Bereiche zusammen: oben die Werkzeuge (Metronom), links die Bibliothek, rechts der Übungsbereich (Player),
/// unten die Statusleiste, dazu die Einstellungen. Führt außerdem die Tastenkürzel aus.
/// </summary>
public sealed partial class MainWindowViewModel : ObservableObject
{
    /// <summary>Schrittweiten der Tastenkürzel.</summary>
    private const double TempoStepPercent = 5;
    private const double VolumeStepDb = 1;
    private const int BpmStep = 1;
    private const int BpmLargeStep = 10;

    public MainWindowViewModel(
        FileBrowserViewModel browser,
        PlayerViewModel player,
        MetronomeViewModel metronome,
        TimerViewModel timer,
        StatusBarViewModel status,
        SettingsViewModel settings,
        LibraryPane libraryPane)
    {
        LibraryPane = libraryPane;
        Browser = browser;
        Player = player;
        Metronome = metronome;
        Timer = timer;
        Status = status;
        Settings = settings;
        Browser.PropertyChanged += OnBrowserChanged;
        Browser.ReleaseSelection = Player.CloseAsync;
        Browser.RevealRequested += (_, _) => LibraryPane.Show();
    }

    public FileBrowserViewModel Browser { get; }

    public PlayerViewModel Player { get; }

    public MetronomeViewModel Metronome { get; }

    /// <summary>Der Übetimer im Werkzeugbereich.</summary>
    public TimerViewModel Timer { get; }

    public StatusBarViewModel Status { get; }

    public SettingsViewModel Settings { get; }

    /// <summary>Breite und Einklappen der Bibliothek.</summary>
    public LibraryPane LibraryPane { get; }

    /// <summary>
    /// Das Metronom ist zum Einstellen aufgeklappt (Tempo, Taktart, Lautstärke). In der Werkzeugleiste steht sonst nur
    /// das Nötigste: Tempo, Schlaganzeige, Start/Stopp.
    /// </summary>
    [ObservableProperty]
    public partial bool IsMetronomeOpen { get; set; }

    /// <summary>Der Timer ist zum Einstellen aufgeklappt (Dauer, Reset).</summary>
    [ObservableProperty]
    public partial bool IsTimerOpen { get; set; }

    /// <summary>Das Fenster soll die Einstellungen öffnen.</summary>
    public event EventHandler? SettingsRequested;

    /// <summary>Das Fenster soll ins Vollbild wechseln oder es verlassen.</summary>
    public event EventHandler? FullScreenToggleRequested;

    [RelayCommand]
    private void OpenMetronome()
    {
        IsTimerOpen = false;
        IsMetronomeOpen = true;
    }

    [RelayCommand]
    private void CloseMetronome() => IsMetronomeOpen = false;

    [RelayCommand]
    private void OpenTimer()
    {
        IsMetronomeOpen = false;
        IsTimerOpen = true;
    }

    [RelayCommand]
    private void CloseTimer() => IsTimerOpen = false;

    /// <summary>Klappt jedes aufgeklappte Werkzeug zu; gibt zurück, ob eines offen war.</summary>
    public bool CloseTools()
    {
        var open = IsMetronomeOpen || IsTimerOpen;
        IsMetronomeOpen = false;
        IsTimerOpen = false;
        return open;
    }

    [RelayCommand]
    private void ToggleFullScreen() => FullScreenToggleRequested?.Invoke(this, EventArgs.Empty);

    /// <summary>Hält die Anzeigen aktuell; die Oberfläche ruft das etwa 30-mal pro Sekunde auf.</summary>
    public void Tick()
    {
        Player.Tick();
        Metronome.Tick();
        Timer.Tick();
        Status.Tick();
    }

    [RelayCommand]
    private void OpenSettings() => SettingsRequested?.Invoke(this, EventArgs.Empty);

    /// <summary>Führt ein Tastenkürzel aus, egal welcher Bereich den Fokus hat.</summary>
    public void Execute(ShortcutAction action)
    {
        switch (action)
        {
            case ShortcutAction.PlayPause:
                Player.PlayPause();
                break;
            case ShortcutAction.ToStart:
                Player.ToStart();
                break;
            case ShortcutAction.SeekBack:
                Player.SeekBy(-PlayerViewModel.SeekSeconds);
                break;
            case ShortcutAction.SeekForward:
                Player.SeekBy(PlayerViewModel.SeekSeconds);
                break;
            case ShortcutAction.TempoDown:
                Player.ChangeTempo(-TempoStepPercent);
                break;
            case ShortcutAction.TempoUp:
                Player.ChangeTempo(TempoStepPercent);
                break;
            case ShortcutAction.VolumeUp:
                Player.ChangeVolume(VolumeStepDb);
                break;
            case ShortcutAction.VolumeDown:
                Player.ChangeVolume(-VolumeStepDb);
                break;
            case ShortcutAction.MetronomeStartStop:
                Metronome.StartStop();
                break;
            case ShortcutAction.MetronomeFaster:
                Metronome.ChangeTempo(BpmStep);
                break;
            case ShortcutAction.MetronomeSlower:
                Metronome.ChangeTempo(-BpmStep);
                break;
            case ShortcutAction.MetronomeMuchFaster:
                Metronome.ChangeTempo(BpmLargeStep);
                break;
            case ShortcutAction.MetronomeMuchSlower:
                Metronome.ChangeTempo(-BpmLargeStep);
                break;
            case ShortcutAction.OpenSettings:
                OpenSettings();
                break;
            case ShortcutAction.ToggleFullScreen:
                ToggleFullScreen();
                break;
            case ShortcutAction.TimerStartPause:
                Timer.StartPause();
                break;
            case ShortcutAction.TimerReset:
                Timer.Reset();
                break;
            case ShortcutAction.TimerLonger:
                Timer.ChangeMinutes(1);
                break;
            case ShortcutAction.TimerShorter:
                Timer.ChangeMinutes(-1);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(action), action, null);
        }
    }

    /// <summary>Eine im Browser gewählte Datei lädt der Player.</summary>
    private void OnBrowserChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(FileBrowserViewModel.SelectedFile) && Browser.SelectedFile is { } file)
        {
            _ = Player.OpenAsync(file);
        }
    }
}
