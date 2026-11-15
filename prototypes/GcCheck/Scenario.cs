using System.Globalization;
using System.Runtime;
using System.Runtime.InteropServices;
using System.Text;

namespace JustPractise.Prototypes.GcCheck;

/// <summary>Eine Phase des automatischen Laufs: welche Last die Oberfläche erzeugt.</summary>
internal sealed record Phase(
    string Name,
    double MegabytesPerSecond,
    bool LargeObjects,
    double RetainedMillions,
    GCLatencyMode LatencyMode,
    double FullGcEverySeconds);

/// <summary>Messwerte einer Phase.</summary>
internal sealed record PhaseResult(
    string Name,
    long Callbacks,
    long Late,
    double MaxGap,
    long OverBudget,
    double MaxBlock,
    double StartupMaxBlock,
    int Gen0,
    int Gen1,
    int Gen2,
    double MaxEphemeralPause,
    double MaxBackgroundPause,
    double MaxFullPause,
    double HeapMegabytes);

internal static class Scenario
{
    public static readonly Phase[] Phases =
    [
        new("Start und Ruhe", 0, false, 0, GCLatencyMode.Interactive, 0),
        new("300 MB/s", 300, false, 0, GCLatencyMode.Interactive, 0),
        new("+ 8 Mio. Objekte, große Objekte", 300, true, 8, GCLatencyMode.Interactive, 0),
        new("+ voller GC alle 4 s", 300, true, 8, GCLatencyMode.Interactive, 4),
        new("SustainedLowLatency", 300, true, 8, GCLatencyMode.SustainedLowLatency, 0),
        new("SustainedLowLatency + voller GC", 300, true, 8, GCLatencyMode.SustainedLowLatency, 4),
    ];

    /// <summary>Hängt die Ergebnisse eines Laufs als Markdown-Tabelle an den Bericht an.</summary>
    public static void AppendReport(string path, string title, IReadOnlyList<PhaseResult> results)
    {
        var culture = CultureInfo.InvariantCulture;
        var text = new StringBuilder();
        text.AppendLine(culture, $"## {title}");
        text.AppendLine();
        text.AppendLine("| Phase | Callbacks | zu spät | größte Lücke ms | über Budget | max Block ms | GC 0/1/2 | max Pause ephemer/Hintergr./voll ms | Heap MB |");
        text.AppendLine("|---|---|---|---|---|---|---|---|---|");
        foreach (var r in results)
        {
            text.AppendLine(culture,
                $"| {r.Name} | {r.Callbacks} | {r.Late} | {r.MaxGap:0.0} | {r.OverBudget} | {r.MaxBlock:0.00} | " +
                $"{r.Gen0}/{r.Gen1}/{r.Gen2} | {r.MaxEphemeralPause:0.0}/{r.MaxBackgroundPause:0.0}/{r.MaxFullPause:0.0} | {r.HeapMegabytes:0} |");
        }

        if (results.Count > 0)
        {
            text.AppendLine();
            text.AppendLine(culture, $"Längster Block in der ersten Sekunde nach dem Start: {results[0].StartupMaxBlock:0.00} ms");
        }

        text.AppendLine();
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.AppendAllText(path, text.ToString());
        Console.Write(text.ToString());
    }

    public static string SystemDescription =>
        $"{RuntimeInformation.OSDescription}, {RuntimeInformation.ProcessArchitecture}, .NET {Environment.Version}";
}
