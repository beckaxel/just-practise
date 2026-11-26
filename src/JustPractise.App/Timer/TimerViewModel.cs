using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JustPractise.App.Controls;
using JustPractise.Core.Settings;
using JustPractise.Core.Timer;
using JustPractise.Storage;

namespace JustPractise.App.Timer;

/// <summary>
/// Der Übetimer im Werkzeugbereich: Fortschrittsbalken mit den verbleibenden Minuten und Start/Pause, abgelaufen
/// Neustart. Aufgeklappt zeigt der Regler die aktuellen Minuten; eine Änderung gilt sofort, auch während er läuft, so
/// lässt er sich beliebig verkürzen oder verlängern. Die zuletzt gesetzten Minuten stehen in den App-Einstellungen; ob
/// er lief, wird nicht gemerkt. Er macht nie Geräusche.
/// </summary>
/// <remarks>Aufrufe kommen vom UI-Thread. Die Anzeige holt <see cref="Tick"/>, das die Oberfläche regelmäßig aufruft.</remarks>
public sealed partial class TimerViewModel : ObservableObject
{
    public const int MinMinutes = TimerSettings.MinMinutes;
    public const int MaxMinutes = TimerSettings.MaxMinutes;

    private static readonly int[] PresetMinutes = [5, 10, 15, 20, 30];

    private readonly PracticeTimer _timer;
    private readonly DataFileKeeper<AppSettings> _settings;
    private int _currentMinutes;

    public TimerViewModel(TimeProvider time, DataFileKeeper<AppSettings> settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        _settings = settings;
        _timer = new PracticeTimer(time, settings.Value.Timer.EffectiveMinutes);
        Presets = [.. PresetMinutes.Select(minutes => new NumberChoice(minutes))];
        Tick();
    }

    /// <summary>
    /// Die aktuellen Minuten, wie sie der Balken zeigt (aufgerundet, in den letzten 30 Sekunden 1). Setzen gilt sofort:
    /// Der Timer läuft von dort aus weiter, und die Minuten gelten auch für den nächsten Start.
    /// </summary>
    public int CurrentMinutes
    {
        get => _currentMinutes;
        set
        {
            var minutes = Math.Clamp(value, MinMinutes, MaxMinutes);
            if (minutes == _currentMinutes && Phase != TimerPhase.Finished)
            {
                return; // Der Regler spiegelt nur die Anzeige zurück.
            }

            _timer.SetMinutes(minutes);
            Save(minutes);
            Tick();
        }
    }

    /// <summary>Die zuletzt gesetzten Minuten; mit ihnen startet der Timer nach dem Ablaufen neu.</summary>
    public int Minutes => _timer.Minutes;

    /// <summary>Voreinstellungen 5, 10, 15, 20, 30 Minuten.</summary>
    public IReadOnlyList<NumberChoice> Presets { get; }

    /// <summary>Die Anzeige im Balken, z. B. „25 min“ oder in den letzten 30 Sekunden „30 s“.</summary>
    [ObservableProperty]
    public partial string DisplayText { get; private set; } = "";

    /// <summary>Wie viel noch übrig ist, von 1 bis 0; der Balken leert sich wie eine Sanduhr.</summary>
    [ObservableProperty]
    public partial double Fraction { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsReady), nameof(IsRunningPhase), nameof(IsPaused), nameof(IsLastSeconds), nameof(IsFinished), nameof(ShowsPlay))]
    public partial TimerPhase Phase { get; private set; }

    public bool IsReady => Phase == TimerPhase.Ready;

    public bool IsRunningPhase => Phase == TimerPhase.Running;

    public bool IsPaused => Phase == TimerPhase.Paused;

    public bool IsLastSeconds => Phase == TimerPhase.LastSeconds;

    public bool IsFinished => Phase == TimerPhase.Finished;

    /// <summary>Läuft gerade; der Knopf zeigt dann Pause.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsPlay))]
    public partial bool IsRunning { get; private set; }

    /// <summary>Der Knopf zeigt Abspielen: bereit oder pausiert.</summary>
    public bool ShowsPlay => !IsRunning && !IsFinished;

    /// <summary>Holt den Stand; die Oberfläche ruft das etwa 30-mal pro Sekunde auf.</summary>
    public void Tick()
    {
        Phase = _timer.Phase;
        IsRunning = _timer.IsRunning;
        Fraction = Phase == TimerPhase.Finished ? 1 : _timer.Fraction; // abgelaufen: voller Balken in der Farbe „geschafft“
        DisplayText = _timer.Display + (_timer.DisplaysSeconds ? " s" : " min");

        var minutes = Math.Max(MinMinutes, _timer.DisplayMinutes);
        if (minutes != _currentMinutes)
        {
            _currentMinutes = minutes;
            OnPropertyChanged(nameof(CurrentMinutes));
            MarkPresets();
        }
    }

    /// <summary>Start oder Pause; ist er abgelaufen, startet er mit den zuletzt gesetzten Minuten neu.</summary>
    [RelayCommand]
    public void StartPause()
    {
        _timer.StartOrPause();
        Tick();
    }

    /// <summary>Zurück auf die zuletzt gesetzten Minuten, angehalten (Tastenkürzel Umschalt+T).</summary>
    public void Reset()
    {
        _timer.Reset();
        Tick();
    }

    /// <summary>Verlängert oder verkürzt um <paramref name="minutes"/>, von den aktuellen Minuten aus.</summary>
    public void ChangeMinutes(int minutes) => CurrentMinutes = Math.Clamp(_currentMinutes + minutes, MinMinutes, MaxMinutes);

    [RelayCommand]
    private void UsePreset(NumberChoice choice) => CurrentMinutes = choice.Value;

    private void Save(int minutes) => _settings.Update(settings =>
    {
        var timer = settings.Timer.WithMinutes(minutes);
        return timer == settings.Timer ? settings : settings with { Timer = timer };
    });

    private void MarkPresets()
    {
        foreach (var preset in Presets ?? [])
        {
            preset.IsSelected = preset.Value == _currentMinutes;
        }
    }
}
