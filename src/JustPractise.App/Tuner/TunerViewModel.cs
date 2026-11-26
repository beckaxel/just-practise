using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JustPractise.App.Localization;
using JustPractise.Audio.Tuner;
using JustPractise.Core.Settings;
using JustPractise.Core.Tuner;
using JustPractise.Storage;

namespace JustPractise.App.Tuner;

/// <summary>Eine Stimmung zur Auswahl: Name (z. B. „Es“) und die Halbtöne, die der notierte Ton über dem klingenden liegt.</summary>
public sealed partial class TranspositionChoice(int semitones) : ObservableObject
{
    public int Semitones { get; } = semitones;

    [ObservableProperty]
    public partial string Label { get; set; } = "";

    [ObservableProperty]
    public partial bool IsSelected { get; set; }
}

/// <summary>
/// Das Stimmgerät im Werkzeugbereich: chromatisch, zeigt den Ton, wie er in den Noten des Instruments steht, und als
/// Balken die Abweichung nach oben oder unten. Das Mikrofon ist nur offen, solange es an ist; beim Start ist es aus.
/// Aufgeklappt gibt es die Einstellungen: Stimmung, Kammerton, Eingabegerät und Vorzeichen.
/// </summary>
/// <remarks>
/// Aufrufe kommen vom UI-Thread; gemessen wird im <see cref="PitchTracker"/>, <see cref="Tick"/> holt das Ergebnis ab.
/// Die Abweichung wird leicht geglättet, damit die Anzeige ruhig steht; bei einem neuen Ton springt sie sofort.
/// </remarks>
public sealed partial class TunerViewModel : ObservableObject, IDisposable
{
    public const double MinReferenceHz = TunerSettings.MinReferenceHz;
    public const double MaxReferenceHz = TunerSettings.MaxReferenceHz;

    /// <summary>Gängige Stimmungen: C (klingend), B, Es, F, A.</summary>
    private static readonly int[] CommonTranspositions = [0, 2, 9, 7, 3];

    /// <summary>So lange bleibt der letzte Ton stehen, wenn kurz keiner erkannt wird (Atem, Zungenstoß).</summary>
    private static readonly TimeSpan Hold = TimeSpan.FromMilliseconds(400);

    /// <summary>Anteil einer neuen Messung an der angezeigten Abweichung; kleiner ist ruhiger, größer schneller.</summary>
    private const double Smoothing = 0.25;

    /// <summary>
    /// Erst ab dieser Entfernung vom angezeigten Ton (in Cent) wechselt die Anzeige sofort auf den Nachbarton. Liegt
    /// ein Ton genau zwischen zwei Halbtönen, springt sie so nicht hin und her.
    /// </summary>
    private const double SwitchCents = 65;

    /// <summary>Pegel unterhalb dieses Werts (dBFS) zeigt der Pegelbalken leer.</summary>
    private const double LevelFloorDb = -60;

    /// <summary>Wie schnell der Pegelbalken fällt (Anteil pro Messung); steigen tut er sofort.</summary>
    private const double LevelRelease = 0.15;

    /// <summary>Bis zu dieser Abweichung gilt der Ton als sauber.</summary>
    private const double InTuneCents = 5;

    private readonly PitchTracker _tracker;
    private readonly DataFileKeeper<AppSettings> _settings;
    private readonly DataFileKeeper<DeviceMemory> _memory;
    private readonly Translator _translator;
    private readonly TimeProvider _time;
    private long _lastMeasured = -1;
    private long _lastPitchAt;
    private int _midi = int.MinValue;
    private int _pending = int.MinValue;
    private double _cents;

    public TunerViewModel(PitchTracker tracker, DataFileKeeper<AppSettings> settings, DataFileKeeper<DeviceMemory> memory, Translator translator, TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(translator);
        _tracker = tracker;
        _settings = settings;
        _memory = memory;
        _translator = translator;
        _time = time;
        var saved = settings.Value;
        ReferenceHz = saved.Tuner.EffectiveReferenceHz;
        Accidentals = saved.Language.EffectiveAccidentals;
        Transpositions = [.. CommonTranspositions.Select(semitones => new TranspositionChoice(semitones))];
        Transposition = saved.Tuner.EffectiveTransposeSemitones;
        Relabel();
        translator.PropertyChanged += OnTranslatorChanged;
    }

    /// <summary>Das Mikrofon ist offen und das Stimmgerät misst.</summary>
    [ObservableProperty]
    public partial bool IsOn { get; private set; }

    /// <summary>Das Eingabegerät ließ sich nicht öffnen (kein Mikrofon oder keine Freigabe).</summary>
    [ObservableProperty]
    public partial bool IsUnavailable { get; private set; }

    /// <summary>Es gibt gerade einen erkannten Ton.</summary>
    [ObservableProperty]
    public partial bool HasNote { get; private set; }

    /// <summary>Der Ton, wie er in den Noten steht, z. B. „Fis“; ohne Ton „–“.</summary>
    [ObservableProperty]
    public partial string NoteName { get; private set; } = "–";

    /// <summary>Die Abweichung von −1 (50 Cent zu tief) bis +1 (50 Cent zu hoch), geglättet.</summary>
    [ObservableProperty]
    public partial double Deviation { get; private set; }

    /// <summary>Die Abweichung als Text, z. B. „+3“.</summary>
    [ObservableProperty]
    public partial string CentsText { get; private set; } = "";

    /// <summary>Der Pegel am Eingang von 0 (still, unter −60 dBFS) bis 1 (voll); steigt sofort und fällt langsam.</summary>
    [ObservableProperty]
    public partial double Level { get; private set; }

    /// <summary>Höchstens 5 Cent daneben.</summary>
    [ObservableProperty]
    public partial bool IsInTune { get; private set; }

    /// <summary>Bei einer transponierenden Stimmung ein Hinweis wie „in Es“, sonst leer.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasHint))]
    public partial string Hint { get; private set; } = "";

    public bool HasHint => Hint.Length > 0;

    public IReadOnlyList<TranspositionChoice> Transpositions { get; }

    /// <summary>Halbtöne zwischen notiertem und klingendem Ton; 0 ist C (klingend).</summary>
    [ObservableProperty]
    public partial int Transposition { get; set; }

    /// <summary>Kammerton in Hz, 415 bis 466.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ReferenceText))]
    public partial double ReferenceHz { get; set; }

    public string ReferenceText => $"{ReferenceHz:0} Hz";


    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(UsesSharps), nameof(UsesFlats))]
    public partial Accidentals Accidentals { get; private set; }

    public bool UsesSharps => Accidentals == Accidentals.Sharp;

    public bool UsesFlats => Accidentals == Accidentals.Flat;

    /// <summary>Die Eingabegeräte; der erste Eintrag ist das Standardgerät des Systems.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<string> InputDevices { get; private set; } = [];

    /// <summary>Das gewählte Eingabegerät; der erste Eintrag (Standard) wird als „kein bestimmtes“ gespeichert.</summary>
    [ObservableProperty]
    public partial string? InputDevice { get; set; }

    /// <summary>Beim Aufklappen: die Geräte neu auflisten, es könnte eines angesteckt worden sein.</summary>
    public void RefreshDevices()
    {
        var standard = _translator.Get("tuner.defaultDevice");
        InputDevices = [standard, .. _tracker.DeviceNames()];
        InputDevice = _memory.Value.InputDevice is { } saved && InputDevices.Contains(saved) ? saved : standard;
    }

    /// <summary>Schaltet das Stimmgerät ein oder aus (Taste S). Eingeschaltet ist das Mikrofon offen.</summary>
    [RelayCommand]
    public void Toggle()
    {
        if (IsOn)
        {
            _tracker.Stop();
            IsOn = false;
            ShowNoNote();
            Level = 0;
            return;
        }

        IsOn = _tracker.Start(_memory.Value.InputDevice);
        IsUnavailable = !IsOn;
        _lastMeasured = -1;
        ShowNoNote();
    }

    /// <summary>Holt die jüngste Messung; die Oberfläche ruft das etwa 30-mal pro Sekunde auf.</summary>
    public void Tick()
    {
        if (!IsOn)
        {
            return;
        }

        var measured = _tracker.Measured;
        if (measured == _lastMeasured)
        {
            return;
        }

        _lastMeasured = measured;
        var reading = _tracker.Latest;
        ShowLevel(reading.LevelDb);
        if (reading.HasPitch)
        {
            Show(TunedNote.ExactMidi(reading.Frequency, ReferenceHz));
        }
        else if (HasNote && _time.GetElapsedTime(_lastPitchAt) > Hold)
        {
            ShowNoNote();
        }
    }

    [RelayCommand]
    private void UseTransposition(TranspositionChoice choice) => Transposition = choice.Semitones;

    [RelayCommand]
    private void UseSharps() => SetAccidentals(Accidentals.Sharp);

    [RelayCommand]
    private void UseFlats() => SetAccidentals(Accidentals.Flat);

    [RelayCommand]
    private void ResetReference() => ReferenceHz = TunerSettings.DefaultReferenceHz;

    public void Dispose()
    {
        _translator.PropertyChanged -= OnTranslatorChanged;
        _tracker.Dispose();
    }

    /// <summary>
    /// Zeigt eine Messung (genaue Tonhöhe als MIDI-Zahl). Der angezeigte Ton bleibt, solange die Messung höchstens
    /// <see cref="SwitchCents"/> davon entfernt ist; ein anderer Ton gilt sofort, wenn er weiter weg ist, sonst erst,
    /// wenn er zweimal hintereinander kommt.
    /// </summary>
    private void Show(double exact)
    {
        _lastPitchAt = _time.GetTimestamp();
        var nearest = (int)Math.Round(exact, MidpointRounding.AwayFromZero);
        var offset = (exact - _midi) * 100;
        if (_midi == int.MinValue || Math.Abs(offset) > SwitchCents || (nearest != _midi && nearest == _pending))
        {
            _midi = nearest;
            _cents = (exact - nearest) * 100;
            _pending = int.MinValue;
        }
        else
        {
            _pending = nearest != _midi ? nearest : int.MinValue;
            _cents += Smoothing * (offset - _cents);
        }

        var written = new TunedNote(_midi, _cents).Written(Transposition);
        HasNote = true;
        NoteName = written.Name(Naming, Accidentals);
        Deviation = Math.Clamp(_cents / 50, -1, 1);
        CentsText = written.CentsText;
        IsInTune = Math.Abs(_cents) <= InTuneCents;
    }

    private void ShowLevel(double levelDb)
    {
        var level = Math.Clamp((levelDb - LevelFloorDb) / -LevelFloorDb, 0, 1);
        Level = level >= Level ? level : Level + (LevelRelease * (level - Level));
    }

    private void ShowNoNote()
    {
        _midi = int.MinValue;
        _pending = int.MinValue;
        HasNote = false;
        NoteName = "–";
        Deviation = 0;
        CentsText = "";
        IsInTune = false;
    }

    private NoteNaming Naming => _translator.Language == "de" ? NoteNaming.German : NoteNaming.English;

    private void SetAccidentals(Accidentals accidentals)
    {
        Accidentals = accidentals;
        _settings.Update(value =>
        {
            var language = value.Language.WithAccidentals(accidentals);
            return language == value.Language ? value : value with { Language = language };
        });
        Relabel();
    }

    /// <summary>Namen der Stimmungen und der Hinweis folgen Sprache und Vorzeichen; B und Es schreiben sich mit b.</summary>
    private void Relabel()
    {
        foreach (var choice in Transpositions)
        {
            // Die Stimmung heißt nach dem klingenden Ton, wenn das Instrument ein notiertes C spielt.
            choice.Label = new TunedNote(-choice.Semitones + 60, 0).Name(Naming, Accidentals.Flat);
            choice.IsSelected = choice.Semitones == Transposition;
        }

        var current = Transpositions.FirstOrDefault(choice => choice.Semitones == Transposition)?.Label;
        Hint = Transposition == 0 || current is null
            ? ""
            : _translator.Format("tuner.inKey", new Dictionary<string, object?> { ["key"] = current });
        if (HasNote)
        {
            NoteName = new TunedNote(_midi, _cents).Written(Transposition).Name(Naming, Accidentals);
        }
    }

    partial void OnTranspositionChanged(int value)
    {
        if (Transpositions is null)
        {
            return; // Im Konstruktor, bevor die Auswahl steht.
        }

        _settings.Update(settings =>
        {
            var tuner = settings.Tuner.WithTransposeSemitones(value);
            return tuner == settings.Tuner ? settings : settings with { Tuner = tuner };
        });
        Relabel();
    }

    partial void OnReferenceHzChanged(double value)
    {
        var clamped = Math.Clamp(Math.Round(value), TunerSettings.MinReferenceHz, TunerSettings.MaxReferenceHz);
        if (clamped != value)
        {
            ReferenceHz = clamped;
            return;
        }

        _settings.Update(settings =>
        {
            var tuner = settings.Tuner.WithReferenceHz(value);
            return tuner == settings.Tuner ? settings : settings with { Tuner = tuner };
        });
    }

    partial void OnInputDeviceChanged(string? value)
    {
        if (value is null || InputDevices.Count == 0)
        {
            return;
        }

        var name = value == InputDevices[0] ? null : value;
        if (name == _memory.Value.InputDevice)
        {
            return;
        }

        _memory.Update(memory => memory with { InputDevice = name });
        if (IsOn)
        {
            // Auf dem neuen Gerät weitermessen.
            Toggle();
            Toggle();
        }
    }

    private void OnTranslatorChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(Translator.Language))
        {
            Relabel();
        }
    }
}
