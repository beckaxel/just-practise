// Prüfpunkt 5: Mikrofon, Eingabegerät, Latenz und stabile Tonhöhenerkennung.
//
//   dotnet run --project prototypes/TunerCheck -- --list                 Eingabegeräte auflisten (öffnet kein Mikrofon)
//   dotnet run --project prototypes/TunerCheck -- --selftest             Erkennung mit erzeugten Tönen, ohne Gerät
//   dotnet run --project prototypes/TunerCheck -- [--device N] [--window 4096] [--seconds 60]
//                                                                        Mikrofon öffnen und live messen (nur der Nutzer)
//
// Live zeigt eine Zeile den erkannten Ton (deutsch, mit Oktave), die Abweichung in Cent mit Nadel, Klarheit und Pegel.
// Alle zwei Sekunden folgt eine Auswertung: Streuung der Cent bei gehaltenem Ton, Anteil erkannter Messungen,
// Rechenzeit pro Messung und Speicherbereinigungen. Es wird nichts aufgenommen oder gespeichert und nichts abgespielt.

using System.Diagnostics;
using System.Globalization;
using JustPractise.Audio.Tuner;
using JustPractise.Core.Tuner;
using SoundFlow.Backends.MiniAudio;
using SoundFlow.Backends.MiniAudio.Devices;
using SoundFlow.Backends.MiniAudio.Enums;
using SoundFlow.Enums;
using SoundFlow.Structs;

CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
const int SampleRate = 48000;

var window = Option("--window", 4096);
if (args.Contains("--selftest"))
{
    SelfTest(window);
    return;
}

using var engine = new MiniAudioEngine();
engine.UpdateAudioDevicesInfo();
var devices = engine.CaptureDevices;
if (args.Contains("--list"))
{
    Console.WriteLine("Eingabegeräte:");
    for (var i = 0; i < devices.Length; i++)
    {
        Console.WriteLine($"  {i}: {devices[i].Name}{(devices[i].IsDefault ? "  (Standard)" : "")}");
    }

    return;
}

var deviceIndex = Option("--device", -1);
DeviceInfo? device = deviceIndex >= 0 && deviceIndex < devices.Length ? devices[deviceIndex] : null;
var seconds = Option("--seconds", 60);
var format = new AudioFormat { Format = SampleFormat.F32, SampleRate = SampleRate, Channels = 1, Layout = AudioFormat.GetLayoutFromChannels(1) };
var config = new MiniAudioDeviceConfig
{
    PeriodSizeInMilliseconds = 10,
    Periods = 2,
    Capture = new DeviceSubConfig { ShareMode = ShareMode.Shared },
};

var ring = new InputRing(SampleRate * 2);
var callbacks = 0L;
using var input = engine.InitializeCaptureDevice(device, format, config);
input.OnAudioProcessed += (samples, _) =>
{
    ring.Write(samples, 1);
    Interlocked.Increment(ref callbacks);
};
input.Start();
Console.WriteLine($"Eingang: {input.Info?.Name ?? "Standard"}, {SampleRate} Hz, Fenster {window} Samples ({window * 1000.0 / SampleRate:0} ms). Strg+C beendet.");

var detector = new PitchDetector(SampleRate, window);
var buffer = new float[window];
var stats = new Stats();
var gcBefore = GC.CollectionCount(0) + GC.CollectionCount(1) + GC.CollectionCount(2);
var started = Stopwatch.StartNew();
var lastReport = 0L;
using var stop = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    stop.Cancel();
};

while (!stop.IsCancellationRequested && started.Elapsed.TotalSeconds < seconds)
{
    Thread.Sleep(40); // 25 Messungen pro Sekunde
    if (!ring.CopyLatest(buffer))
    {
        continue;
    }

    var t0 = Stopwatch.GetTimestamp();
    var reading = detector.Detect(buffer);
    var ms = Stopwatch.GetElapsedTime(t0).TotalMilliseconds;
    stats.Add(reading, ms);
    Console.Write("\r" + Line(reading).PadRight(78));

    if (started.ElapsedMilliseconds - lastReport >= 2000)
    {
        lastReport = started.ElapsedMilliseconds;
        Console.WriteLine();
        Console.WriteLine(stats.Report() + $", Callbacks {Interlocked.Read(ref callbacks)}");
        stats = new Stats();
    }
}

input.Stop();
var gcAfter = GC.CollectionCount(0) + GC.CollectionCount(1) + GC.CollectionCount(2);
Console.WriteLine();
Console.WriteLine($"Ende nach {started.Elapsed.TotalSeconds:0} s, Speicherbereinigungen: {gcAfter - gcBefore}");

static string Line(PitchReading reading)
{
    var level = $"{reading.LevelDb,6:0.0} dB";
    if (!reading.HasPitch)
    {
        return $"   –                                         Klarheit {reading.Clarity:0.00}  {level}";
    }

    var note = TunedNote.FromFrequency(reading.Frequency);
    var name = $"{note.Name(NoteNaming.German, Accidentals.Sharp)}{note.Octave}";
    var needle = new char[21];
    Array.Fill(needle, '·');
    needle[10] = '|';
    needle[Math.Clamp(10 + (int)Math.Round(note.Cents / 5), 0, 20)] = '●';
    return $"{name,-5} {note.CentsText,4} ct [{new string(needle)}] {reading.Frequency,8:0.00} Hz  Klarheit {reading.Clarity:0.00}  {level}";
}

int Option(string name, int fallback)
{
    var index = Array.IndexOf(args, name);
    return index >= 0 && index + 1 < args.Length && int.TryParse(args[index + 1], out var value) ? value : fallback;
}

static void SelfTest(int window)
{
    var detector = new PitchDetector(SampleRate, window);
    Console.WriteLine($"Selbsttest ohne Gerät, Fenster {window} Samples ({window * 1000.0 / SampleRate:0} ms)");
    Console.WriteLine("  Ton            soll Hz     ist Hz   Abw. ct  Klarheit");
    var random = new Random(1);
    (string Name, double Hz, double[] Harmonics, double Noise)[] cases =
    [
        ("E1 Bass", 41.2034, [0.5, 1, 0.7, 0.5], 0.01),
        ("E2 Gitarre", 82.4069, [0.4, 1, 0.8, 0.6, 0.5, 0.3], 0.01),
        ("B3 Tenorsax", 233.0819, [1, 0.9, 0.7, 0.5, 0.4, 0.3, 0.2], 0.02),
        ("A4", 440, [1, 0.5, 0.25], 0.0),
        ("A4 +7 ct", 440 * Math.Pow(2, 7 / 1200.0), [1, 0.5, 0.25], 0.02),
        ("E5 Geige", 659.2551, [1, 0.8, 0.6, 0.5, 0.4], 0.02),
        ("C7 Piccolo", 2093.005, [1, 0.2], 0.01),
        ("Rauschen", 0, [], 0.3),
    ];
    foreach (var (name, hz, harmonics, noise) in cases)
    {
        var samples = new float[window];
        for (var i = 0; i < window; i++)
        {
            var t = (double)i / SampleRate;
            var value = 0.0;
            for (var h = 0; h < harmonics.Length; h++)
            {
                value += harmonics[h] * Math.Sin((2 * Math.PI * hz * (h + 1) * t) + h);
            }

            samples[i] = (float)((0.2 * value) + (noise * ((random.NextDouble() * 2) - 1)));
        }

        var reading = detector.Detect(samples);
        var off = reading.HasPitch && hz > 0 ? 1200 * Math.Log2(reading.Frequency / hz) : double.NaN;
        Console.WriteLine($"  {name,-13} {hz,8:0.00} {reading.Frequency,10:0.00} {off,9:0.00}  {reading.Clarity,8:0.00}");
    }

    var buffer = new float[window];
    for (var i = 0; i < window; i++)
    {
        buffer[i] = (float)Math.Sin(2 * Math.PI * 220 * i / SampleRate);
    }

    for (var i = 0; i < 50; i++)
    {
        detector.Detect(buffer); // aufwärmen
    }

    var gcBefore = GC.CollectionCount(0);
    var watch = Stopwatch.StartNew();
    const int Runs = 1000;
    for (var i = 0; i < Runs; i++)
    {
        detector.Detect(buffer);
    }

    Console.WriteLine($"Rechenzeit: {watch.Elapsed.TotalMilliseconds / Runs:0.000} ms pro Messung, " +
        $"bei 25 Messungen/s {watch.Elapsed.TotalMilliseconds / Runs * 25 / 10:0.0} % eines Kerns, " +
        $"Gen0-Bereinigungen in {Runs} Messungen: {GC.CollectionCount(0) - gcBefore}");
}

/// <summary>Auswertung über zwei Sekunden.</summary>
internal sealed class Stats
{
    private readonly List<double> _times = [];
    private readonly Dictionary<int, List<double>> _cents = [];
    private int _readings;
    private int _pitched;

    public void Add(PitchReading reading, double ms)
    {
        _readings++;
        _times.Add(ms);
        if (!reading.HasPitch)
        {
            return;
        }

        _pitched++;
        var note = TunedNote.FromFrequency(reading.Frequency);
        if (!_cents.TryGetValue(note.Midi, out var list))
        {
            _cents[note.Midi] = list = [];
        }

        list.Add(note.Cents);
    }

    public string Report()
    {
        var held = _cents.OrderByDescending(pair => pair.Value.Count).FirstOrDefault();
        var spread = held.Value is { Count: > 2 } values ? StdDev(values) : double.NaN;
        var name = held.Value is not null ? new TunedNote(held.Key, 0).Name(NoteNaming.German, Accidentals.Sharp) + new TunedNote(held.Key, 0).Octave : "–";
        return $"  erkannt {_pitched}/{_readings}, häufigster Ton {name} ({held.Value?.Count ?? 0}×), Streuung {spread:0.0} ct, " +
            $"Rechenzeit Ø {(_times.Count > 0 ? _times.Average() : 0):0.00} ms / max {(_times.Count > 0 ? _times.Max() : 0):0.00} ms";
    }

    private static double StdDev(List<double> values)
    {
        var mean = values.Average();
        return Math.Sqrt(values.Sum(v => (v - mean) * (v - mean)) / values.Count);
    }
}
