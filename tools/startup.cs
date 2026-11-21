// Measures startup time and package size of just practise with and without ReadyToRun and as a single file (check 9).
// The app reports the time since process start at several marks (Shell/StartupProbe.cs).
// Publishes the app self-contained for this machine into artifacts/startup/<variant>/, starts each
// variant several times and writes artifacts/startup/report.md. The app opens its window and the
// audio device, but plays nothing, and closes itself after the first frame.
//
//   dotnet run tools/startup.cs                  publish both variants, 10 starts each
//   dotnet run tools/startup.cs -- --runs 20     more starts
//   dotnet run tools/startup.cs -- --no-publish  measure the existing publish output again

using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text;

var runs = 10;
var publish = true;
for (var i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--runs" when i + 1 < args.Length && int.TryParse(args[i + 1], CultureInfo.InvariantCulture, out var n) && n > 0:
            runs = n;
            i++;
            break;
        case "--no-publish":
            publish = false;
            break;
        default:
            Console.WriteLine($"Unknown option {args[i]}.");
            Console.WriteLine("Usage: dotnet run tools/startup.cs [-- --runs 10] [--no-publish]");
            return 2;
    }
}

var root = FindRoot();
if (root is null)
{
    Console.WriteLine("Run this from inside the just-practise repository.");
    return 1;
}

var rid = RuntimeInformation.RuntimeIdentifier;
var outputRoot = Path.Combine(root, "artifacts", "startup");
Variant[] variants =
[
    new("jit", "without ReadyToRun", ["-p:PublishReadyToRun=false"]),
    new("r2r", "with ReadyToRun", ["-p:PublishReadyToRun=true"]),
    new("single", "single file, compressed, with ReadyToRun", ["-p:PublishReadyToRun=true", "-p:PublishSingleFile=true", "-p:IncludeNativeLibrariesForSelfExtract=true", "-p:EnableCompressionInSingleFile=true"]),
];

if (publish)
{
    foreach (var variant in variants)
    {
        var directory = Path.Combine(outputRoot, variant.Name);
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }

        string[] arguments =
        [
            "publish", Path.Combine("src", "JustPractise.App"), "-c", "Release", "-r", rid, "--self-contained",
            "-o", directory, .. variant.PublishArguments,
        ];
        if (Run(root, "dotnet", arguments) != 0)
        {
            Console.WriteLine($"Publishing {variant.Name} failed.");
            return 1;
        }
    }
}

var executableName = OperatingSystem.IsWindows() ? "just-practise.exe" : "just-practise";
var results = new List<Result>();
foreach (var variant in variants)
{
    var directory = Path.Combine(outputRoot, variant.Name);
    if (!File.Exists(Path.Combine(directory, executableName)))
    {
        Console.WriteLine($"{directory} has no {executableName}. Run without --no-publish.");
        return 1;
    }

    results.Add(new Result(variant, DirectorySize(directory), ZipSize(directory), []));
}

// Abwechselnd starten, damit beide Varianten gleich unter Hintergrundlast und Cache leiden.
// Der erste Start jeder Variante zählt extra, er liest die Dateien oft noch von der Platte.
for (var run = 0; run <= runs; run++)
{
    foreach (var result in results)
    {
        var sample = Measure(Path.Combine(outputRoot, result.Variant.Name, executableName));
        if (sample is null)
        {
            return 1;
        }

        result.Samples.Add(sample);
        Console.WriteLine($"{result.Variant.Name} #{run}: {sample}");
    }
}

var report = Report(rid, runs, results);
var reportFile = Path.Combine(outputRoot, "report.md");
File.WriteAllText(reportFile, report);
Console.WriteLine();
Console.WriteLine(report);
Console.WriteLine($"Written to {reportFile}");
return 0;

static Sample? Measure(string executable)
{
    var file = Path.GetTempFileName();
    try
    {
        var info = new ProcessStartInfo(executable) { UseShellExecute = false };
        info.Environment["JUST_PRACTISE_STARTUP_PROBE"] = file;
        using var process = Process.Start(info);
        if (process is null || !process.WaitForExit(TimeSpan.FromSeconds(60)))
        {
            process?.Kill(entireProcessTree: true);
            Console.WriteLine("The app did not close within 60 s.");
            return null;
        }

        var line = File.ReadAllText(file).Trim();
        if (line.Length == 0)
        {
            Console.WriteLine($"The app exited with code {process.ExitCode} without writing its startup time.");
            return null;
        }

        var marks = line.Split(' ')
            .Select(part => part.Split('='))
            .Select(pair => new Mark(pair[0], double.Parse(pair[1], CultureInfo.InvariantCulture)))
            .ToList();
        return new Sample(marks);
    }
    finally
    {
        File.Delete(file);
    }
}

static string Report(string rid, int runs, List<Result> results)
{
    var text = new StringBuilder();
    text.AppendLine(CultureInfo.InvariantCulture, $"# Startup time and package size ({rid}, {DateTime.Now:yyyy-MM-dd})");
    text.AppendLine();
    text.AppendLine(CultureInfo.InvariantCulture, $"{Environment.OSVersion}, {Environment.ProcessorCount} logical cores. Times in ms since process start, median of {runs} starts; the first start of each variant is listed separately.");
    text.AppendLine();
    text.AppendLine("| Variant | Size | Zip | First start (frame) | Fastest frame | Slowest frame |");
    text.AppendLine("|---|---|---|---|---|---|");
    foreach (var result in results)
    {
        var rest = result.Samples.Skip(1).ToList();
        text.AppendLine(CultureInfo.InvariantCulture,
            $"| {result.Variant.Description} | {Megabytes(result.Size)} | {Megabytes(result.ZipSize)} | {result.Samples[0].Frame:F0} | {rest.Min(s => s.Frame):F0} | {rest.Max(s => s.Frame):F0} |");
    }

    // Messpunkte als Zeilen: Zeitpunkt (Median) und Abstand zum vorherigen Punkt.
    var names = results[0].Samples[0].Marks.Select(mark => mark.Name).ToList();
    text.AppendLine();
    text.AppendLine("| Mark | " + string.Join(" | ", results.Select(result => $"{result.Variant.Description} (since previous)")) + " |");
    text.AppendLine("|---|" + string.Concat(results.Select(_ => "---|")));
    foreach (var name in names)
    {
        var cells = results.Select(result =>
        {
            var rest = result.Samples.Skip(1).ToList();
            var at = Median(rest, sample => sample.At(name));
            var index = names.IndexOf(name);
            var step = index == 0 ? at : at - Median(rest, sample => sample.At(names[index - 1]));
            return string.Create(CultureInfo.InvariantCulture, $"{at:F0} (+{step:F0})");
        });
        text.AppendLine(CultureInfo.InvariantCulture, $"| {name} | {string.Join(" | ", cells)} |");
    }

    return text.ToString();
}

static double Median(List<Sample> samples, Func<Sample, double> value)
{
    var sorted = samples.Select(value).Order().ToList();
    return sorted.Count % 2 == 1 ? sorted[sorted.Count / 2] : (sorted[(sorted.Count / 2) - 1] + sorted[sorted.Count / 2]) / 2;
}

static string Megabytes(long bytes) => string.Create(CultureInfo.InvariantCulture, $"{bytes / 1024.0 / 1024.0:F1} MB");

static long DirectorySize(string directory) =>
    new DirectoryInfo(directory).EnumerateFiles("*", SearchOption.AllDirectories).Sum(file => file.Length);

static long ZipSize(string directory)
{
    var file = Path.GetTempFileName();
    try
    {
        File.Delete(file);
        ZipFile.CreateFromDirectory(directory, file, CompressionLevel.SmallestSize, includeBaseDirectory: false);
        return new FileInfo(file).Length;
    }
    finally
    {
        File.Delete(file);
    }
}

static int Run(string workingDirectory, string file, string[] arguments)
{
    Console.WriteLine($"> {file} {string.Join(' ', arguments)}");
    try
    {
        using var process = Process.Start(new ProcessStartInfo(file, arguments) { WorkingDirectory = workingDirectory, UseShellExecute = false });
        if (process is null)
        {
            return -1;
        }

        process.WaitForExit();
        return process.ExitCode;
    }
    catch (Win32Exception exception)
    {
        Console.WriteLine(exception.Message);
        return -1;
    }
}

static string? FindRoot()
{
    for (var directory = new DirectoryInfo(Environment.CurrentDirectory); directory is not null; directory = directory.Parent)
    {
        if (File.Exists(Path.Combine(directory.FullName, "JustPractise.slnx")))
        {
            return directory.FullName;
        }
    }

    return null;
}

internal sealed record Variant(string Name, string Description, string[] PublishArguments);

internal sealed record Mark(string Name, double Milliseconds);

internal sealed record Sample(List<Mark> Marks)
{
    public double Frame => At("frame");

    public double At(string name) => Marks.First(mark => mark.Name == name).Milliseconds;

    public override string ToString() =>
        string.Join(", ", Marks.Select(mark => string.Create(CultureInfo.InvariantCulture, $"{mark.Name} {mark.Milliseconds:F0}")));
}

internal sealed record Result(Variant Variant, long Size, long ZipSize, List<Sample> Samples);
