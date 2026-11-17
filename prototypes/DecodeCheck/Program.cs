using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using JustPractise.Prototypes.DecodeCheck;
using SoundFlow.Backends.MiniAudio;

// Prüfpunkt 7: Speicher bei langen Stücken. Ganz decodieren oder streamen? Gibt keinen Ton aus.
//
//   dotnet run -c Release --project prototypes/DecodeCheck -- <datei>... [--folder ordner] [--count n] [--gc-seconds s] [--out ordner]
//
// --folder nimmt die n größten Audiodateien eines Ordners (Standard 3). Der Bericht landet in decodecheck-out/report.md.

var culture = CultureInfo.InvariantCulture;
var files = new List<string>();
string? folder = null;
var count = 3;
var gcSeconds = 10.0;
var outDir = "decodecheck-out";
for (var i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--folder" when i + 1 < args.Length:
            folder = args[++i];
            break;
        case "--count" when i + 1 < args.Length:
            count = int.Parse(args[++i], culture);
            break;
        case "--gc-seconds" when i + 1 < args.Length:
            gcSeconds = double.Parse(args[++i], culture);
            break;
        case "--out" when i + 1 < args.Length:
            outDir = args[++i];
            break;
        case var arg when arg.StartsWith("--", StringComparison.Ordinal):
            Console.WriteLine($"Unbekannte Option {arg}");
            return 1;
        default:
            files.Add(args[i]);
            break;
    }
}

if (folder is not null)
{
    string[] extensions = [".mp3", ".wav", ".flac", ".ogg"];
    files.AddRange(Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories)
        .Where(f => extensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
        .Select(f => new FileInfo(f))
        .OrderByDescending(f => f.Length)
        .Take(count)
        .Select(f => f.FullName));
}

if (files.Count == 0)
{
    Console.WriteLine("Aufruf: <datei>... [--folder ordner] [--count n] [--gc-seconds s] [--out ordner]");
    return 1;
}

var report = new Report();
report.Line("# Prüfpunkt 7: Speicher bei langen Stücken");
report.Line();
report.Line($"- System: {RuntimeInformation.OSDescription}, {RuntimeInformation.OSArchitecture}, {Environment.ProcessorCount} Kerne, .NET {Environment.Version}");
report.Line($"- {GcMonitor.Mode}, Arbeitsspeicher gesamt {GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / 1e9:0.0} GB");
report.Line($"- Datum: {DateTime.Now:yyyy-MM-dd HH:mm}");

using var engine = new MiniAudioEngine();
var pool = new BlockPool();
WarmUp(engine, files[0]);

foreach (var file in files)
{
    Console.WriteLine($"Datei: {file}");
    report.Line();
    report.Line($"## {Path.GetFileName(file)}");
    report.Line();

    // Erst short, dann float: Das float-Array bleibt danach als Referenz für die Sprünge stehen.
    var (tagStats, tagSamples) = FullDecoder.DecodeFloatWithTags(engine, file);
    GC.KeepAlive(tagSamples);
    tagSamples = null;
    var (shortStats, shortSamples) = FullDecoder.DecodeInt16(engine, file);
    GC.KeepAlive(shortSamples);
    shortSamples = null;
    var (floatStats, floatSamples) = FullDecoder.DecodeFloat(engine, file);
    var createdBefore = pool.Created;
    var (blockStats, blocks) = FullDecoder.DecodeBlocks(engine, file, pool);
    var identical = SameAudio(blocks, floatSamples, floatStats.Frames);
    var blockCount = blocks.Blocks;
    blocks.Release();

    report.Line($"{new FileInfo(file).Length / 1e6:0.0} MB auf der Platte, {floatStats.Seconds / 60:0.0} min, " +
        $"decodiert mit {floatStats.SampleRate} Hz, {floatStats.Channels} Kanäle ({floatStats.Detected}).");
    var estimated = floatStats.EstimatedSamples / floatStats.Channels;
    report.Line($"Geschätzte Länge vorab {estimated} Frames, tatsächlich {floatStats.Frames} ({estimated - floatStats.Frames:+0;-0;0}).");
    report.Line();
    report.Line("### Ganz decodieren");
    report.Line();
    report.Line("| Speicherform | Öffnen | erste 5 s | ganz | Echtzeit | Vergrößert | Puffer | angelegt gesamt | Gen2 | blockierend | max Pause |");
    report.Line("|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
    foreach (var s in new[] { floatStats, blockStats, shortStats, tagStats })
    {
        report.Line($"| {s.Storage} | {s.OpenMilliseconds:0} ms | {s.FirstSecondsMilliseconds:0} ms | {s.TotalMilliseconds:0} ms | " +
            $"{s.RealtimeFactor:0}x | {s.Resizes}x | {s.BufferMegabytes:0} MB | {s.AllocatedMegabytes:0} MB | " +
            $"{s.Gen2Collections} | {s.FullBlocking} | {s.MaxPauseMilliseconds:0.0} ms |");
    }

    report.Line();
    report.Line($"Mit Tags lesen: {tagStats.SampleRate} Hz ({tagStats.Detected}).");
    report.Line();
    report.Line($"Blöcke aus Pool: {blockCount} Blöcke à {BlockPool.BlockSamples * sizeof(float) / 1e6:0.0} MB, davon {pool.Created - createdBefore} neu angelegt " +
        $"(Pool jetzt {pool.Created} Blöcke), Inhalt {(identical ? "identisch mit dem Array" : "WEICHT AB")}.");
    report.Line();
    var peakMs = Peaks(floatSamples, floatStats.Frames, floatStats.Channels);
    report.Line($"Wellenform-Spitzen (je 256 Frames) aus dem decodierten Puffer: {peakMs:0.0} ms.");
    report.Line();

    Console.WriteLine("  Sprünge beim Streamen ...");
    var seeks = SeekCheck.Run(engine, file, floatSamples, floatStats.Frames, floatStats.Channels, floatStats.SampleRate);
    report.Line("### Streamen: Sprünge");
    report.Line();
    report.Line("Reihenfolge wie beim Üben. „Versatz“ ist die Abweichung vom Ziel in Frames, gefunden durch Vergleich mit dem ganz decodierten Signal; " +
        "„Position“ ist, was der Decoder selbst meldet. „Abweichung“ ist die größte Differenz eines Samples nach dem Ausrichten, „davor“ die in den ersten 2048 Frames nach dem Sprung.");
    report.Line();
    report.Line("| Ziel | Sprung | erster 10-ms-Block | Position | Versatz | Abweichung | davor |");
    report.Line("|--:|--:|--:|--:|--:|--:|--:|");
    foreach (var r in seeks)
    {
        var offset = r.OffsetFrames is { } o ? $"{o:+0;-0;0}" : "kein Treffer";
        report.Line($"| {r.TargetSeconds:0.0} s | {r.SeekMilliseconds:0.00} ms | {r.FirstBlockMilliseconds:0.00} ms | {r.ReportedOffsetFrames:+0;-0;0} | " +
            $"{offset} | {r.MaxDifference:0.000000} | {r.MaxDifferenceStart:0.000000} |");
    }

    GC.KeepAlive(floatSamples);
}

Console.WriteLine($"Speicherbereinigung mit großem Puffer ({gcSeconds:0} s je Szenario):");
report.Line();
report.Line($"## Speicherbereinigung mit großem Puffer ({GcScenarios.Minutes} min, 48 kHz, Stereo)");
report.Line();
report.Line($"Je {gcSeconds:0} s Last wie eine geschäftige Oberfläche (100 MB/s, 500 000 dauerhaft lebende Objekte). " +
    "„Anlegen“ ist die Zeit für Anlegen und Beschreiben des Puffers, beim Dateiwechsel der längste Wechsel. " +
    "Heap und Arbeitsspeicher am Ende des Szenarios; sie enthalten Reste der vorherigen Szenarien, die .NET nicht sofort freigibt.");
report.Line();
report.Line("| Szenario | Puffer | Anlegen | Heap | Arbeitsspeicher | Gen0 | Gen1 | Gen2 | blockierend | max klein | max Hintergrund | max blockierend | Pausen gesamt |");
report.Line("|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
foreach (var r in new GcScenarios(gcSeconds).Run())
{
    report.Line($"| {r.Name} | {r.BufferMegabytes:0} MB | {r.AllocateMilliseconds:0} ms | {r.HeapMegabytes:0} MB | {r.WorkingSetMegabytes:0} MB | " +
        $"{r.Gen0} | {r.Gen1} | {r.Gen2} | {r.FullBlocking} | {r.MaxEphemeral:0.0} ms | {r.MaxBackground:0.0} ms | {r.MaxFullBlocking:0.0} ms | {r.TotalPause:0} ms |");
}

Directory.CreateDirectory(outDir);
var path = Path.Combine(outDir, "report.md");
File.WriteAllText(path, report.ToString());
Console.WriteLine();
Console.WriteLine(report.ToString());
Console.WriteLine($"Bericht: {Path.GetFullPath(path)}");
return 0;

// Einmal ein Stück decodieren, damit JIT und das Laden der nativen Bibliothek nicht in die erste Messung fallen
static void WarmUp(MiniAudioEngine engine, string file)
{
    using var stream = File.OpenRead(file);
    using var provider = FullDecoder.Open(engine, stream);
    var buffer = new float[16384];
    for (var i = 0; i < 20 && provider.ReadBytes(buffer) > 0; i++)
    {
    }
}

// Liest die Blöcke in 10-ms-Stücken wie der Audio-Callback und vergleicht mit dem ganzen Array
static bool SameAudio(BlockAudio blocks, float[] reference, long frames)
{
    if (blocks.Frames != frames)
    {
        return false;
    }

    var buffer = new float[441 * blocks.Channels];
    for (long frame = 0; frame < frames; frame += 441)
    {
        var read = blocks.Read(frame, buffer);
        if (!buffer.AsSpan(0, read).SequenceEqual(reference.AsSpan((int)(frame * blocks.Channels), read)))
        {
            return false;
        }
    }

    return true;
}

static double Peaks(float[] samples, long frames, int channels)
{
    const int FramesPerPeak = 256;
    var start = Stopwatch.GetTimestamp();
    var peaks = new float[(frames + FramesPerPeak - 1) / FramesPerPeak];
    for (long p = 0; p < peaks.Length; p++)
    {
        var from = (int)(p * FramesPerPeak * channels);
        var to = (int)Math.Min((p + 1) * FramesPerPeak, frames) * channels;
        var max = 0f;
        foreach (var sample in samples.AsSpan(from, to - from))
        {
            max = Math.Max(max, Math.Abs(sample));
        }

        peaks[p] = max;
    }

    GC.KeepAlive(peaks);
    return Stopwatch.GetElapsedTime(start).TotalMilliseconds;
}

/// <summary>Sammelt den Bericht als Markdown.</summary>
internal sealed class Report
{
    private readonly StringBuilder _text = new();

    public void Line(string text = "") => _text.Append(text).Append('\n');

    public override string ToString() => _text.ToString();
}
