using System.Diagnostics;
using System.Globalization;
using Avalonia;

namespace JustPractise.Prototypes.GcCheck;

// Prüfpunkt 1: Laufen Wiedergabe, Time-Stretching und Metronom ohne Aussetzer,
// während die Oberfläche zeichnet und Speicher anlegt?
//
//   dotnet run -c Release --project prototypes/GcCheck -- <datei> --matrix      alle Einstellungen automatisch
//   dotnet run -c Release --project prototypes/GcCheck -- <datei> --auto        ein automatischer Lauf
//   dotnet run -c Release --project prototypes/GcCheck -- <datei> [--buffer ms] von Hand bedienen
//
// Weitere Optionen: --start s, --seconds s, --no-warmup, --cheaper, --phase-seconds s, --report datei
// Die Art der Speicherbereinigung wird über Umgebungsvariablen gewählt:
//   DOTNET_gcServer=1        Server-GC
//   DOTNET_gcConcurrent=0    ohne Hintergrund-GC
internal static class Program
{
    private static readonly (string Name, uint Buffer, bool WarmUp, string? Variable)[] MatrixRuns =
    [
        ("Standard", 10, true, null),
        ("ohne Vorrechnen", 10, false, null),
        ("ohne Hintergrund-GC", 10, true, "DOTNET_gcConcurrent=0"),
        ("Server-GC", 10, true, "DOTNET_gcServer=1"),
        ("Puffer 5 ms", 5, true, null),
        ("Puffer 20 ms", 20, true, null),
    ];

    [STAThread]
    public static int Main(string[] args)
    {
        var options = Options.Parse(args);
        if (options is null)
        {
            Console.WriteLine(Options.Usage);
            return 1;
        }

        if (options.Matrix)
        {
            return RunMatrix(options);
        }

        Console.WriteLine($"{GcMonitor.Mode}, Puffer {options.BufferMilliseconds} ms, " +
            $"Vorrechnen {(options.WarmUp ? "an" : "aus")}{(options.StretchOptions != 0 ? ", cheaper" : "")}");

        using var session = new AudioSession(options);
        Console.WriteLine($"{Path.GetFileName(options.File)}: {session.Clip.Seconds:0.0} s, Ausgabe {session.DeviceName}");

        App.Session = session;
        App.Options = options;
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace()
            .StartWithClassicDesktopLifetime(args);
        return 0;
    }

    /// <summary>Startet das Programm nacheinander mit jeder Einstellung als eigenen Prozess (GC-Art wirkt nur beim Start).</summary>
    private static int RunMatrix(Options options)
    {
        var report = Path.GetFullPath(options.Report);
        Directory.CreateDirectory(Path.GetDirectoryName(report)!);
        var minutes = MatrixRuns.Length * Scenario.Phases.Length * options.PhaseSeconds / 60;
        File.WriteAllText(report,
            $"# GcCheck {DateTime.Now:yyyy-MM-dd HH:mm}\n\n{Scenario.SystemDescription}\n\n" +
            $"Datei: {Path.GetFileName(options.File)}, {options.PhaseSeconds.ToString(CultureInfo.InvariantCulture)} s pro Phase\n\n");
        Console.WriteLine($"{MatrixRuns.Length} Läufe, zusammen etwa {minutes:0} Minuten. Bericht: {report}");

        var executable = Path.Combine(AppContext.BaseDirectory, OperatingSystem.IsWindows() ? "GcCheck.exe" : "GcCheck");
        var failed = 0;
        for (var i = 0; i < MatrixRuns.Length; i++)
        {
            var run = MatrixRuns[i];
            Console.WriteLine();
            Console.WriteLine($"=== Lauf {i + 1}/{MatrixRuns.Length}: {run.Name} ===");
            var info = new ProcessStartInfo(executable, (options with { Report = report }).ToAutoArguments(run.Buffer, run.WarmUp))
            {
                UseShellExecute = false,
            };
            if (run.Variable?.Split('=') is [var name, var value])
            {
                info.Environment[name] = value;
            }

            using var process = Process.Start(info);
            process?.WaitForExit();
            if (process is null || process.ExitCode != 0)
            {
                Console.WriteLine($"Lauf {run.Name} ist fehlgeschlagen.");
                failed++;
            }
        }

        Console.WriteLine();
        Console.WriteLine($"Fertig. Bericht: {report}");
        return failed == 0 ? 0 : 1;
    }
}
