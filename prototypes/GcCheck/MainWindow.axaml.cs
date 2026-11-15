using System.Diagnostics;
using System.Globalization;
using System.Runtime;
using System.Text;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace JustPractise.Prototypes.GcCheck;

/// <summary>
/// Bedienung und Anzeige. Ein Prototyp, daher Code-Behind statt MVVM.
/// Das Protokoll schreibt eine Zeile, sobald ein Callback zu spät kam, ein Block über Budget lag
/// oder ein GC länger als 5 ms angehalten hat, damit sich Aussetzer und GCs zuordnen lassen.
/// </summary>
internal sealed partial class MainWindow : Window
{
    private const double NoticeablePauseMilliseconds = 5.0;
    private const int MaxLogLines = 400;

    private readonly AudioSession? _session;
    private readonly Options? _options;
    private readonly Stopwatch _phaseClock = new();
    private readonly List<PhaseResult> _results = [];
    private readonly List<string> _marks = [];
    private readonly PipeWireMonitor? _pipeWire;
    private Dictionary<string, long> _pipeWireAtPhaseStart = [];
    private int _heard;
    private int _phaseIndex = -1;
    private double _nextFullGc;
    private readonly LoadGenerator _load = new();
    private readonly GcMonitor _gc = new();
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly List<string> _log = [];
    private readonly int[] _logCounts = new int[3];
    private readonly DispatcherTimer _timer;
    private TimeSpan _lastFrame;
    private long _lastLate;
    private long _lastOverBudget;

    public MainWindow()
    {
        InitializeComponent();
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
    }

    public MainWindow(AudioSession session, Options options, PipeWireMonitor pipeWire)
        : this()
    {
        _session = session;
        _pipeWire = pipeWire;
        _options = options;
        Header.Text = $"{Path.GetFileName(options.File)}\n{session.DeviceName}, Puffer {options.BufferMilliseconds} ms\n" +
            $"{GcMonitor.Mode}, Vorrechnen {(options.WarmUp ? "an" : "aus")}" +
            (options.StretchOptions != 0 ? ", cheaper" : "");

        LatencyBox.ItemsSource = new[]
        {
            GCLatencyMode.Interactive, GCLatencyMode.SustainedLowLatency, GCLatencyMode.Batch, GCLatencyMode.LowLatency,
        };
        LatencyBox.SelectedItem = GCSettings.LatencyMode;

        PlayButton.Click += OnPlayClick;
        HeardButton.Click += (_, _) => MarkHeard();

        // Tunnel-Handler: Die Leertaste markiert immer und löst nie den gerade fokussierten Knopf aus.
        AddHandler(KeyDownEvent, OnPreviewKeyDown, RoutingStrategies.Tunnel);
        ClickBox.IsCheckedChanged += (_, _) => session.Click.Running = ClickBox.IsChecked == true;
        TempoSlider.ValueChanged += (_, _) => session.Player.Tempo = (float)(TempoSlider.Value / 100);
        BpmSlider.ValueChanged += (_, _) => session.Click.Bpm = (int)BpmSlider.Value;
        AllocationSlider.ValueChanged += (_, _) => _load.MegabytesPerSecond = AllocationSlider.Value;
        SurvivalSlider.ValueChanged += (_, _) => _load.SurvivalPercent = SurvivalSlider.Value;
        LargeBox.IsCheckedChanged += (_, _) => _load.LargeObjects = LargeBox.IsChecked == true;
        LinesSlider.ValueChanged += (_, _) => Load.Lines = (int)LinesSlider.Value;
        RetainedSlider.ValueChanged += (_, _) => UpdateLabels();
        RetainedButton.Click += OnRetainedClick;
        LatencyBox.SelectionChanged += OnLatencyChanged;
        CollectButton.Click += OnCollectClick;
        ResetButton.Click += (_, _) => ResetStats();

        session.Player.Tempo = (float)(TempoSlider.Value / 100);
        session.Click.Bpm = (int)BpmSlider.Value;
        session.Player.Playing = true;
        _load.SurvivalPercent = SurvivalSlider.Value;
        Load.Lines = (int)LinesSlider.Value;
        UpdateLabels();

        if (options.Auto)
        {
            ClickBox.IsChecked = true;
        }

        _timer.Tick += (_, _) => OnTimer();
        _timer.Start();
        RequestAnimationFrame(OnFrame);
    }

    protected override void OnClosed(EventArgs e)
    {
        _timer.Stop();
        base.OnClosed(e);
    }

    private void OnFrame(TimeSpan time)
    {
        var elapsed = _lastFrame == TimeSpan.Zero ? TimeSpan.Zero : time - _lastFrame;
        _lastFrame = time;
        _load.Tick(elapsed);
        Load.Advance(elapsed);
        RequestAnimationFrame(OnFrame);
    }

    private void OnPreviewKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Space)
        {
            MarkHeard();
            e.Handled = true;
        }
    }

    private void MarkHeard()
    {
        _heard++;
        var phase = _phaseIndex >= 0 ? Scenario.Phases[_phaseIndex].Name : "–";
        var mark = string.Create(CultureInfo.CurrentCulture,
            $"Phase {_phaseIndex + 1} „{phase}“, {_phaseClock.Elapsed.TotalSeconds:0.0} s nach Phasenbeginn");
        _marks.Add(mark);
        AddLog($"AUSSETZER GEHÖRT: {mark}");
    }

    private void OnPlayClick(object? sender, RoutedEventArgs e)
    {
        if (_session is null)
        {
            return;
        }

        _session.Player.Playing = !_session.Player.Playing;
        PlayButton.Content = _session.Player.Playing ? "Pause" : "Play";
    }

    private void OnRetainedClick(object? sender, RoutedEventArgs e)
    {
        var count = (int)(RetainedSlider.Value * 1_000_000);
        var watch = Stopwatch.StartNew();
        _load.SetRetainedObjects(count);
        AddLog($"{count / 1e6:0.0} Mio. Objekte dauerhaft angelegt in {watch.ElapsedMilliseconds} ms, Heap {GcMonitor.HeapMegabytes:0} MB");
        UpdateLabels();
    }

    private void OnLatencyChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (LatencyBox.SelectedItem is GCLatencyMode mode)
        {
            GCSettings.LatencyMode = mode;
            AddLog($"GCLatencyMode = {GCSettings.LatencyMode}");
        }
    }

    private void OnCollectClick(object? sender, RoutedEventArgs e)
    {
        var watch = Stopwatch.StartNew();
        GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);
        AddLog($"Voller GC per Knopf: {watch.Elapsed.TotalMilliseconds:0.0} ms");
    }

    private void ResetStats()
    {
        if (_session is null)
        {
            return;
        }

        _session.Player.Stats.Reset();
        _session.Callbacks.Reset();
        _gc.Reset();
        Array.Clear(_logCounts);
        _lastLate = 0;
        _lastOverBudget = 0;
        _clock.Restart();
        AddLog("Zurückgesetzt");
    }

    private void OnTimer()
    {
        if (_session is null)
        {
            return;
        }

        var pause = _gc.Poll();
        var late = _session.Callbacks.Late;
        var overBudget = _session.Player.Stats.OverBudget;
        var gap = _session.Callbacks.TakeIntervalMaxMilliseconds();
        var block = _session.Player.Stats.TakeIntervalMaxMilliseconds();

        if (late > _lastLate || overBudget > _lastOverBudget || pause >= NoticeablePauseMilliseconds)
        {
            var counts = new int[3];
            for (var generation = 0; generation < 3; generation++)
            {
                counts[generation] = _gc.Count(generation) - _logCounts[generation];
                _logCounts[generation] = _gc.Count(generation);
            }

            AddLog(string.Create(CultureInfo.CurrentCulture,
                $"spät +{late - _lastLate} (Lücke {gap:0.0} ms), über Budget +{overBudget - _lastOverBudget} (Block {block:0.0} ms), " +
                $"GC +{counts[0]}/{counts[1]}/{counts[2]}, Pause {pause:0.0} ms"));
            _lastLate = late;
            _lastOverBudget = overBudget;
        }

        UpdateStats();
        UpdateLabels();
        if (_options?.Auto == true)
        {
            RunScenario();
        }
    }

    /// <summary>Automatischer Lauf: stellt die Last jeder Phase ein, misst und schreibt am Ende den Bericht.</summary>
    private void RunScenario()
    {
        if (_phaseIndex < 0)
        {
            StartPhase(0);
            return;
        }

        var phase = Scenario.Phases[_phaseIndex];
        var elapsed = _phaseClock.Elapsed.TotalSeconds;
        if (phase.FullGcEverySeconds > 0 && elapsed >= _nextFullGc)
        {
            OnCollectClick(null, new RoutedEventArgs());
            _nextFullGc += phase.FullGcEverySeconds;
        }

        if (elapsed < _options!.PhaseSeconds)
        {
            return;
        }

        _gc.Poll();
        _results.Add(Snapshot(phase.Name));
        if (_phaseIndex + 1 < Scenario.Phases.Length)
        {
            StartPhase(_phaseIndex + 1);
            return;
        }

        _timer.Stop();
        var title = $"{GcMonitor.Mode}, Puffer {_options.BufferMilliseconds} ms, " +
            $"Vorrechnen {(_options.WarmUp ? "an" : "aus")}{(_options.StretchOptions != 0 ? ", cheaper" : "")}, " +
            $"Ausgabe {_session!.DeviceName}";
        Scenario.AppendReport(_options.Report, title, _results, _marks);
        Close();
    }

    private void StartPhase(int index)
    {
        var phase = Scenario.Phases[index];
        _phaseIndex = index;
        AddLog($"Phase {index + 1}/{Scenario.Phases.Length}: {phase.Name}");
        AllocationSlider.Value = phase.MegabytesPerSecond;
        LargeBox.IsChecked = phase.LargeObjects;
        LatencyBox.SelectedItem = phase.LatencyMode;
        var retained = (int)(phase.RetainedMillions * 1_000_000);
        if (retained != _load.RetainedObjects)
        {
            RetainedSlider.Value = phase.RetainedMillions;
            OnRetainedClick(null, new RoutedEventArgs());
        }

        // Die erste Phase bleibt ungeleert, damit sie die Spitzen direkt nach dem Start enthält.
        if (index > 0)
        {
            ResetStats();
        }

        _nextFullGc = phase.FullGcEverySeconds;
        _heard = 0;
        _pipeWireAtPhaseStart = _pipeWire?.Snapshot() ?? [];
        _phaseClock.Restart();
    }

    private PhaseResult Snapshot(string name)
    {
        var player = _session!.Player.Stats;
        var callbacks = _session.Callbacks;
        var (pipeWireErrors, pipeWireNodes) = _pipeWire?.Since(_pipeWireAtPhaseStart) ?? (0, "");
        return new PhaseResult(
            name,
            callbacks.Count,
            callbacks.Late,
            callbacks.MaxGapMilliseconds,
            player.OverBudget,
            player.MaxMilliseconds,
            player.StartupMaxMilliseconds,
            _gc.Count(0),
            _gc.Count(1),
            _gc.Count(2),
            _gc.MaxEphemeralPause,
            _gc.MaxBackgroundPause,
            _gc.MaxFullBlockingPause,
            GcMonitor.HeapMegabytes,
            _heard,
            _pipeWire?.IsAvailable == true ? pipeWireErrors : null,
            pipeWireNodes);
    }

    private void UpdateStats()
    {
        if (_session is null)
        {
            return;
        }

        var player = _session.Player.Stats;
        var callbacks = _session.Callbacks;
        var text = new StringBuilder();
        text.AppendLine(CultureInfo.CurrentCulture, $"Laufzeit       {_clock.Elapsed.TotalSeconds:0} s, Position {_session.Player.Seconds:0.0} s");
        text.AppendLine(CultureInfo.CurrentCulture, $"Callbacks      {callbacks.Count}, je {callbacks.BufferMilliseconds:0.0} ms");
        text.AppendLine(CultureInfo.CurrentCulture, $"  zu spät      {callbacks.Late}, größte Lücke {callbacks.MaxGapMilliseconds:0.0} ms");
        text.AppendLine(CultureInfo.CurrentCulture, $"Blöcke         Ø {player.AverageMilliseconds:0.00} ms, max {player.MaxMilliseconds:0.00} ms");
        text.AppendLine(CultureInfo.CurrentCulture, $"  Start (1 s)  max {player.StartupMaxMilliseconds:0.00} ms");
        text.AppendLine(CultureInfo.CurrentCulture, $"  über Budget  {player.OverBudget}");
        text.AppendLine(CultureInfo.CurrentCulture, $"GCs            gen0 {_gc.Count(0)}, gen1 {_gc.Count(1)}, gen2 {_gc.Count(2)}");
        text.AppendLine(CultureInfo.CurrentCulture, $"  Pause gesamt {_gc.TotalPauseMilliseconds:0.0} ms");
        text.AppendLine(CultureInfo.CurrentCulture, $"  max ephemer  {_gc.MaxEphemeralPause:0.0} ms");
        text.AppendLine(CultureInfo.CurrentCulture, $"  max Hintergr {_gc.MaxBackgroundPause:0.0} ms");
        text.AppendLine(CultureInfo.CurrentCulture, $"  max voll     {_gc.MaxFullBlockingPause:0.0} ms");
        text.Append(CultureInfo.CurrentCulture, $"Heap           {GcMonitor.HeapMegabytes:0} MB, {GCSettings.LatencyMode}");
        StatsText.Text = text.ToString();
    }

    private void UpdateLabels()
    {
        TempoLabel.Text = $"Tempo {TempoSlider.Value:0} %";
        BpmLabel.Text = $"Metronom {BpmSlider.Value:0} bpm";
        AllocationLabel.Text = $"Speicher anlegen: {AllocationSlider.Value:0} MB/s";
        SurvivalLabel.Text = $"Davon überleben: {SurvivalSlider.Value:0.0} %";
        RetainedLabel.Text = $"Dauerhaft belegt: {RetainedSlider.Value:0.0} Mio. Objekte (aktiv {_load.RetainedObjects / 1e6:0.0})";
        LinesLabel.Text = $"Zeichenlast: {LinesSlider.Value:0} Linien pro Frame";
    }

    private void AddLog(string message)
    {
        var line = string.Create(CultureInfo.CurrentCulture, $"{_clock.Elapsed.TotalSeconds,7:0.0} s  {message}");
        Console.WriteLine(line);
        _log.Add(line);
        if (_log.Count > MaxLogLines)
        {
            _log.RemoveAt(0);
        }

        LogBox.Text = string.Join('\n', _log);
        LogBox.CaretIndex = LogBox.Text.Length;
    }
}
