using System.Globalization;
using JustPractise.App.Localization;
using JustPractise.App.Tuner;
using JustPractise.Audio;
using JustPractise.Audio.Tuner;
using JustPractise.Core.DataFiles;
using JustPractise.Core.Settings;
using JustPractise.Storage;

namespace JustPractise.App.Tests.Tuner;

public sealed class TunerViewModelTests : IAsyncDisposable
{
    private readonly TemporaryDirectory _directory = new();
    private readonly ToneInput _input = new();
    private readonly ManualTime _time = new();
    private readonly PitchTracker _tracker;
    private readonly DataFileKeeper<AppSettings> _settings;
    private readonly DataFileKeeper<DeviceMemory> _memory;
    private readonly Translator _translator = new(TranslationCatalog.Load(null), "de", CultureInfo.GetCultureInfo("de-DE"));

    public TunerViewModelTests()
    {
        _tracker = new PitchTracker(_input, _time);
        _settings = new DataFileKeeper<AppSettings>(new DataFileStore<AppSettings>(_directory.File("settings.json"), AppDataFiles.Settings));
        _memory = new DataFileKeeper<DeviceMemory>(new DataFileStore<DeviceMemory>(_directory.File("device.json"), AppDataFiles.Device));
    }

    public async ValueTask DisposeAsync()
    {
        await _settings.FlushAsync();
        await _memory.FlushAsync();
        _directory.Dispose();
    }

    private TunerViewModel Create() => new(_tracker, _settings, _memory, _translator, _time);

    /// <summary>Misst einmal und gibt das Ergebnis an das ViewModel weiter.</summary>
    private void Measure(TunerViewModel tuner)
    {
        _input.Fill();
        _tracker.Measure();
        tuner.Tick();
    }

    [Fact]
    public void IsOffAtStartAndOpensTheMicrophoneOnlyWhenSwitchedOn()
    {
        var tuner = Create();
        Assert.False(tuner.IsOn);
        Assert.False(_input.IsOpen);
        Assert.Equal("–", tuner.NoteName);

        tuner.ToggleCommand.Execute(null);
        Assert.True(tuner.IsOn);
        Assert.True(_input.IsOpen);

        tuner.ToggleCommand.Execute(null);
        Assert.False(tuner.IsOn);
        Assert.False(_input.IsOpen);
    }

    [Fact]
    public void ShowsTheNoteAndHowFarItIsOff()
    {
        var tuner = Create();
        _input.Frequency = 440 * Math.Pow(2, 20 / 1200.0); // A, 20 Cent zu hoch
        tuner.ToggleCommand.Execute(null);

        Measure(tuner);

        Assert.True(tuner.HasNote);
        Assert.Equal("A", tuner.NoteName);
        Assert.Equal(0.4, tuner.Deviation, 1);
        Assert.Equal("+20", tuner.CentsText);
        Assert.False(tuner.IsInTune);

        _input.Frequency = 440 * Math.Pow(2, -3 / 1200.0);
        for (var i = 0; i < 10; i++)
        {
            Measure(tuner); // geglättet nähert sich die Anzeige an
        }

        Assert.True(tuner.IsInTune);
        Assert.True(tuner.Deviation < 0);
    }

    [Fact]
    public void ShowsTheWrittenNoteOfAnAltoSaxophone()
    {
        var tuner = Create();
        tuner.UseTranspositionCommand.Execute(tuner.Transpositions.Single(choice => choice.Label == "E♭"));
        _input.Frequency = 261.6256; // klingendes C
        tuner.ToggleCommand.Execute(null);

        Measure(tuner);

        Assert.Equal("A", tuner.NoteName); // steht für das Altsax als A in den Noten
        Assert.Equal("in E♭", tuner.Hint);
        Assert.Equal(["C", "B", "E♭", "F", "A"], tuner.Transpositions.Select(choice => choice.Label));
    }

    [Fact]
    public void UsesSharpsByDefaultAndFlatsOnRequest()
    {
        var tuner = Create();
        _input.Frequency = 233.0819; // klingendes Ais/B
        tuner.ToggleCommand.Execute(null);
        Measure(tuner);
        Assert.True(tuner.UsesSharps);
        Assert.Equal("A♯", tuner.NoteName);

        tuner.UseFlatsCommand.Execute(null);

        Assert.Equal("B", tuner.NoteName);
        Assert.Equal(Core.Tuner.Accidentals.Flat, _settings.Value.Language.EffectiveAccidentals);
    }

    [Fact]
    public void NamesNotesInEnglishWhenTheAppIsInEnglish()
    {
        var tuner = Create();
        _input.Frequency = 233.0819;
        tuner.ToggleCommand.Execute(null);
        Measure(tuner);

        _translator.SetLanguage("en");

        Assert.Equal("A♯", tuner.NoteName);
        Assert.Equal(["C", "B♭", "E♭", "F", "A"], tuner.Transpositions.Select(choice => choice.Label));
    }

    [Fact]
    public void FollowsTheReferencePitchAndRemembersIt()
    {
        var tuner = Create();
        tuner.ReferenceHz = 442;
        _input.Frequency = 442;
        tuner.ToggleCommand.Execute(null);
        Measure(tuner);

        Assert.True(tuner.IsInTune);
        Assert.Equal("442 Hz", tuner.ReferenceText);
        Assert.Equal(442, _settings.Value.Tuner.EffectiveReferenceHz);

        tuner.ResetReferenceCommand.Execute(null);
        Assert.Equal(440, tuner.ReferenceHz);
        Assert.True(_settings.Value.Tuner.IsDefault);
    }

    [Fact]
    public void DoesNotFlickerBetweenTwoSemitonesWhenANoteSitsInBetween()
    {
        var tuner = Create();
        _input.Frequency = 440 * Math.Pow(2, 45 / 1200.0); // A, 45 Cent zu hoch
        tuner.ToggleCommand.Execute(null);
        Measure(tuner);
        Assert.Equal("A", tuner.NoteName);

        // Der Ton eiert um die Grenze zum A♯: 52, 48, 55, 49 Cent über A
        foreach (var cents in new[] { 52.0, 48, 55, 49, 53 })
        {
            _input.Frequency = 440 * Math.Pow(2, cents / 1200);
            Measure(tuner);
            Assert.Equal("A", tuner.NoteName);
        }

        Assert.Equal(1, tuner.Deviation, 1); // ganz rechts: so hoch wie es geht
    }

    [Fact]
    public void SwitchesAtOnceWhenTheNextNoteIsClearlyPlayed()
    {
        var tuner = Create();
        _input.Frequency = 440;
        tuner.ToggleCommand.Execute(null);
        Measure(tuner);

        _input.Frequency = 493.8833; // H, deutlich weg vom A
        Measure(tuner);

        Assert.Equal("H", tuner.NoteName);
        Assert.True(tuner.IsInTune);
    }

    [Fact]
    public void ShowsTheLevelRisingAtOnceAndFallingSlowly()
    {
        var tuner = Create();
        _input.Frequency = 440;
        tuner.ToggleCommand.Execute(null);
        Measure(tuner);
        var loud = tuner.Level;
        Assert.True(loud > 0.7);

        _input.Frequency = 0;
        Measure(tuner);
        Assert.InRange(tuner.Level, 0.1, loud - 0.01);

        tuner.ToggleCommand.Execute(null);
        Assert.Equal(0, tuner.Level);
    }

    [Fact]
    public void HoldsTheLastNoteBrieflyWhenTheToneStops()
    {
        var tuner = Create();
        _input.Frequency = 440;
        tuner.ToggleCommand.Execute(null);
        Measure(tuner);

        _input.Frequency = 0; // Stille
        _time.Advance(TimeSpan.FromMilliseconds(200));
        Measure(tuner);
        Assert.True(tuner.HasNote);

        _time.Advance(TimeSpan.FromMilliseconds(300));
        Measure(tuner);
        Assert.False(tuner.HasNote);
        Assert.Equal("–", tuner.NoteName);
    }

    [Fact]
    public void RemembersTheInputDeviceForThisComputerAndListsTheDefaultFirst()
    {
        var tuner = Create();
        tuner.RefreshDevices();
        Assert.Equal(["Standardgerät", "Mikrofon", "Interface"], tuner.InputDevices);
        Assert.Equal("Standardgerät", tuner.InputDevice);

        tuner.InputDevice = "Interface";
        tuner.ToggleCommand.Execute(null);

        Assert.Equal("Interface", _memory.Value.InputDevice);
        Assert.Equal("Interface", _input.OpenedDevice);

        tuner.InputDevice = "Standardgerät";
        Assert.Null(_memory.Value.InputDevice);
        Assert.Null(_input.OpenedDevice); // läuft auf dem Standardgerät weiter
        Assert.True(tuner.IsOn);
    }

    [Fact]
    public void ReportsAMissingMicrophone()
    {
        _input.Fails = true;
        var tuner = Create();

        tuner.ToggleCommand.Execute(null);

        Assert.False(tuner.IsOn);
        Assert.True(tuner.IsUnavailable);
    }

    /// <summary>Ein Eingabegerät, das beim Öffnen einen Sinus der gewünschten Frequenz liefert.</summary>
    private sealed class ToneInput : IAudioInput
    {
        private InputRing? _ring;

        public double Frequency { get; set; } = 440;

        public bool Fails { get; set; }

        public bool IsOpen => _ring is not null;

        public string? OpenedDevice { get; private set; }

        public int SampleRate => 48000;

        public IReadOnlyList<string> DeviceNames() => ["Mikrofon", "Interface"];

        public bool Open(string? deviceName, InputRing ring)
        {
            if (Fails)
            {
                return false;
            }

            OpenedDevice = deviceName;
            _ring = ring;
            return true;
        }

        public void Close() => _ring = null;

        /// <summary>Vor jeder Messung: ein frisches Fenster mit dem aktuellen Ton.</summary>
        public void Fill()
        {
            var samples = new float[4096];
            for (var i = 0; i < samples.Length; i++)
            {
                samples[i] = Frequency > 0 ? (float)(0.3 * Math.Sin(2 * Math.PI * Frequency * i / SampleRate)) : 0;
            }

            _ring?.Write(samples, 1);
        }
    }

    /// <summary>Eine Uhr, die nur auf Anweisung läuft; ihr Takt misst nie von selbst.</summary>
    private sealed class ManualTime : TimeProvider
    {
        private long _ticks;

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override long GetTimestamp() => _ticks;

        public void Advance(TimeSpan by) => _ticks += by.Ticks;

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period) => new NoTimer();

        private sealed class NoTimer : ITimer
        {
            public bool Change(TimeSpan dueTime, TimeSpan period) => true;

            public void Dispose()
            {
            }

            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }
}
