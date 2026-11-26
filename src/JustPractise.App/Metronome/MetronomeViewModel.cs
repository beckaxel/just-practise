using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JustPractise.Audio;
using JustPractise.Core.Metronome;
using JustPractise.Core.Settings;
using JustPractise.Storage;

namespace JustPractise.App.Metronome;

/// <summary>Ein Punkt der Schlaganzeige.</summary>
public sealed partial class BeatDot(int index) : ObservableObject
{
    public int Index { get; } = index;

    /// <summary>Die Eins ist betont.</summary>
    public bool IsAccent => Index == 0;

    [ObservableProperty]
    public partial bool IsCurrent { get; set; }
}

/// <summary>Eine Tempo-Voreinstellung; <see cref="IsSelected"/> markiert die aktuelle.</summary>
public sealed partial class NumberChoice(int value) : ObservableObject
{
    public int Value { get; } = value;

    public string Label => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);

    [ObservableProperty]
    public partial bool IsSelected { get; set; }
}

/// <summary>
/// Das Metronom: Tempo (mit Voreinstellungen), Schläge pro Takt (der erste betont), Lautstärke, Start/Stopp und
/// Schlaganzeige. Die letzte Einstellung
/// steht in den App-Einstellungen und ist beim nächsten Start wieder da; ob es lief, wird nicht gemerkt.
/// </summary>
/// <remarks>Aufrufe kommen vom UI-Thread. Den Schlag holt <see cref="Tick"/>, das die Oberfläche regelmäßig aufruft.</remarks>
public sealed partial class MetronomeViewModel : ObservableObject
{
    public const int MinBpm = MetronomeSetup.MinBpm;
    public const int MaxBpm = MetronomeSetup.MaxBpm;
    public const double MinVolumeDb = -20;
    public const double MaxVolumeDb = 10;

    /// <summary>Schläge pro Takt; jeder Schlag ist ein Klick, der erste ist betont.</summary>
    public const int MinBeats = 1;
    public const int MaxBeats = 16;

    /// <summary>Tempo-Voreinstellungen.</summary>
    private static readonly int[] PresetBpms = [60, 90, 120];

    private readonly IMetronome _metronome;
    private readonly DataFileKeeper<AppSettings> _settings;

    /// <summary>Erst nach dem Konstruktor stellen Änderungen das Metronom ein und werden gespeichert.</summary>
    private readonly bool _ready;

    public MetronomeViewModel(IMetronome metronome, DataFileKeeper<AppSettings> settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        _metronome = metronome;
        _settings = settings;

        // Beim Laden soll nichts geschrieben werden, deshalb ist _ready hier noch false.
        var saved = settings.Value.Metronome;
        Bpm = saved.Current.EffectiveBpm;
        Meter = Normalize(saved.Current.EffectiveMeter);
        TempoPresets = [.. PresetBpms.Select(bpm => new NumberChoice(bpm))];
        MarkChoices();
        VolumeDb = Math.Clamp(saved.EffectiveVolumeDb, MinVolumeDb, MaxVolumeDb);
        Beats = CreateBeats(Meter);
        metronome.SetTempo(Bpm);
        metronome.SetMeter(Meter);
        metronome.SetVolume(VolumeDb);
        _ready = true;
    }

    [ObservableProperty]
    public partial bool IsRunning { get; private set; }

    /// <summary>Tempo in Schlägen pro Minute, 30 bis 250.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BpmInput))]
    public partial int Bpm { get; set; }

    /// <summary>Das Tempo für das Eingabefeld, das mit <see cref="decimal"/> arbeitet; leer bleibt das alte Tempo.</summary>
    public decimal? BpmInput
    {
        get => Bpm;
        set
        {
            if (value is { } bpm)
            {
                Bpm = (int)Math.Round(bpm);
            }
            else
            {
                OnPropertyChanged(); // Feld wieder füllen
            }
        }
    }

    /// <summary>
    /// Die Taktart; eingestellt werden nur die Schläge pro Takt (1 bis 16). Der Nenner bleibt 4 und spielt keine Rolle,
    /// ein Schlag ist ein Klick.
    /// </summary>
    [ObservableProperty]
    public partial Meter Meter { get; private set; }

    /// <summary>Tempo-Voreinstellungen (60, 90, 120 BPM).</summary>
    public IReadOnlyList<NumberChoice> TempoPresets { get; }

    /// <summary>Lautstärke des Metronoms in dB, unabhängig vom Player.</summary>
    [ObservableProperty]
    public partial double VolumeDb { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<BeatDot> Beats { get; private set; }

    /// <summary>Holt den aktuellen Schlag vom Audio-Thread; die Oberfläche ruft das etwa 30-mal pro Sekunde auf.</summary>
    public void Tick()
    {
        IsRunning = _metronome.IsRunning;
        var current = IsRunning ? _metronome.LastBeat?.IndexInBar : null;
        foreach (var beat in Beats)
        {
            beat.IsCurrent = beat.Index == current;
        }
    }

    /// <summary>Ändert das Tempo um <paramref name="bpm"/>, innerhalb von 30 bis 250 BPM.</summary>
    public void ChangeTempo(int bpm) => Bpm = Math.Clamp(Bpm + bpm, MinBpm, MaxBpm);

    [RelayCommand]
    public void StartStop()
    {
        _metronome.SetRunning(!_metronome.IsRunning);
        Tick();
    }

    [RelayCommand]
    private void MoreBeats() => SetMeter(Meter with { Beats = Math.Min(Meter.Beats + 1, MaxBeats) });

    [RelayCommand]
    private void FewerBeats() => SetMeter(Meter with { Beats = Math.Max(Meter.Beats - 1, MinBeats) });

    [RelayCommand]
    private void UseTempoPreset(NumberChoice choice) => Bpm = choice.Value;

    /// <summary>Nur die Schläge zählen; eine gespeicherte Taktart wie 7/8 wird zu 7 Schlägen (7/4), höchstens 16.</summary>
    private static Meter Normalize(Meter meter) => new(Math.Clamp(meter.Beats, MinBeats, MaxBeats), 4);

    private void MarkChoices()
    {
        foreach (var choice in TempoPresets)
        {
            choice.IsSelected = choice.Value == Bpm;
        }
    }

    [RelayCommand]
    private void ResetVolume() => VolumeDb = 0;

    private void SetMeter(Meter meter)
    {
        if (meter == Meter)
        {
            return;
        }

        Meter = meter;
        MarkChoices();
        Beats = CreateBeats(meter);
        _metronome.SetMeter(meter);
        Save(settings => settings with { Current = settings.Current.WithMeter(meter) });
        Tick();
    }

    partial void OnBpmChanged(int value)
    {
        // Werte außerhalb des Bereichs (z. B. aus dem Eingabefeld) werden begrenzt.
        var clamped = Math.Clamp(value, MinBpm, MaxBpm);
        if (clamped != value)
        {
            Bpm = clamped;
            return;
        }

        if (!_ready)
        {
            return;
        }

        MarkChoices();
        _metronome.SetTempo(value);
        Save(settings => settings with { Current = settings.Current.WithBpm(value) });
    }

    partial void OnVolumeDbChanged(double value)
    {
        if (!_ready)
        {
            return;
        }

        _metronome.SetVolume(value);
        Save(settings => settings.WithVolumeDb(value));
    }

    private void Save(Func<MetronomeSettings, MetronomeSettings> change) =>
        _settings.Update(settings => settings with { Metronome = change(settings.Metronome) });

    private static BeatDot[] CreateBeats(Meter meter) => [.. Enumerable.Range(0, meter.Beats).Select(index => new BeatDot(index))];
}
