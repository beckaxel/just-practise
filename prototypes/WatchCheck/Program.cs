using System.Diagnostics;
using System.Globalization;
using JustPractise.Prototypes.WatchCheck;

// Prüfpunkt 8: Dateiüberwachung mit FileSystemWatcher in großen Musikordnern und Cloud-Ordnern. Gibt keinen Ton aus.
//
//   dotnet run -c Release --project prototypes/WatchCheck -- scan <ordner> [--watchers n] [--seconds s]
//       überwacht einen (großen) Ordner rekursiv: Startzeit, Speicher, inotify-Verbrauch, Dauer eines Abfragelaufs
//   dotnet run -c Release --project prototypes/WatchCheck -- stress [testordner]
//       erzeugt Änderungen in Bursts in einem Testordner (Standard: im Temp-Ordner) und prüft, was ankommt; löscht ihn danach
//   dotnet run -c Release --project prototypes/WatchCheck -- listen <ordner>
//       gibt alle Ereignisse mit Zeit aus, bis Enter gedrückt wird (für Tests von Hand, z. B. Cloud-Änderungen)

var culture = CultureInfo.InvariantCulture;
CultureInfo.CurrentCulture = culture;
if (args.Length == 0)
{
    Console.WriteLine("Aufruf: scan <ordner> [--watchers n] [--seconds s] | stress [testordner] | listen <ordner>");
    return 1;
}

Console.WriteLine($"{Environment.OSVersion}, .NET {Environment.Version}");
switch (args[0])
{
    case "scan" when args.Length >= 2:
        return Scan(args[1], Option("--watchers", 1), Option("--seconds", 10));
    case "stress":
        var testRoot = args.Length >= 2 ? args[1] : Path.Combine(Path.GetTempPath(), $"watchcheck-{Environment.ProcessId}");
        var problems = new StressTest(Path.GetFullPath(testRoot), Console.WriteLine).Run();
        Console.WriteLine();
        Console.WriteLine(problems == 0 ? "Alle Schritte vollständig." : $"{problems} Schritte unvollständig.");
        return problems == 0 ? 0 : 2;
    case "listen" when args.Length >= 2:
        return Listen(args[1]);
    default:
        Console.WriteLine($"Unbekannter Aufruf: {string.Join(' ', args)}");
        return 1;
}

int Option(string name, int fallback)
{
    var index = Array.IndexOf(args, name);
    return index >= 0 && index + 1 < args.Length ? int.Parse(args[index + 1], culture) : fallback;
}

static int Scan(string folder, int watcherCount, int seconds)
{
    folder = Path.GetFullPath(folder);
    Console.WriteLine($"Ordner {folder}");
    Console.WriteLine($"vorher   {InotifyStats.Read()}");

    // Abfragen als Ersatz: alle Ordner und Dateien mit Änderungszeitpunkt durchgehen.
    var options = new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true, AttributesToSkip = 0 };
    var directory = new DirectoryInfo(folder);
    for (var pass = 1; pass <= 2; pass++)
    {
        var timer = Stopwatch.StartNew();
        long folders = 0, files = 0, latest = 0;
        foreach (var entry in directory.EnumerateFileSystemInfos("*", options))
        {
            latest = Math.Max(latest, entry.LastWriteTimeUtc.Ticks);
            if (entry is DirectoryInfo)
            {
                folders++;
            }
            else
            {
                files++;
            }
        }

        Console.WriteLine($"Abfragelauf {pass} (alle Einträge mit Änderungszeit): {folders} Ordner, {files} Dateien in {timer.ElapsedMilliseconds} ms");
    }

    {
        var timer = Stopwatch.StartNew();
        var folders = directory.EnumerateDirectories("*", options).Count(sub => sub.LastWriteTimeUtc.Ticks > 0);
        Console.WriteLine($"Abfragelauf nur Ordner (Änderungszeit der Ordner): {folders} Ordner in {timer.ElapsedMilliseconds} ms");
    }

    GC.Collect();
    var managedBefore = GC.GetTotalMemory(forceFullCollection: true);
    var workingSetBefore = Environment.WorkingSet;
    var recorders = new List<EventRecorder>();
    for (var i = 0; i < watcherCount; i++)
    {
        var timer = Stopwatch.StartNew();
        recorders.Add(new EventRecorder(folder, onError: error => Console.WriteLine($"Fehler: {error}")));
        Console.WriteLine($"Watcher {i + 1} gestartet in {timer.ElapsedMilliseconds} ms");
    }

    // Unter Linux legt .NET die Watches für Unterordner im Hintergrund an; kurz warten, bis die Zahl steht.
    Thread.Sleep(2000);
    var managedAfter = GC.GetTotalMemory(forceFullCollection: true);
    Console.WriteLine($"nachher  {InotifyStats.Read()}");
    Console.WriteLine($"Speicher: verwaltet +{(managedAfter - managedBefore) / 1024.0 / 1024.0:0.0} MiB, Working Set +{(Environment.WorkingSet - workingSetBefore) / 1024.0 / 1024.0:0.0} MiB");

    Console.WriteLine($"Lausche {seconds} s ...");
    Thread.Sleep(TimeSpan.FromSeconds(seconds));
    var (events, errors) = recorders[0].Drain();
    Console.WriteLine($"{events.Count} Ereignisse, {errors.Count} Fehler in {seconds} s");
    foreach (var group in events.GroupBy(e => e.Kind))
    {
        Console.WriteLine($"  {group.Key}: {group.Count()}");
    }

    foreach (var recorder in recorders)
    {
        recorder.Dispose();
    }

    return 0;
}

static int Listen(string folder)
{
    folder = Path.GetFullPath(folder);
    Console.WriteLine($"Lausche auf {folder}, Ende mit Enter.");
    var start = DateTime.Now;
    using var recorder = new EventRecorder(folder,
        e => Console.WriteLine($"{DateTime.Now:HH:mm:ss.fff}  {e.Kind,-8} {Path.GetRelativePath(folder, e.Path)}{(e.OldPath is null ? "" : $"  (vorher {Path.GetRelativePath(folder, e.OldPath)})")}"),
        error => Console.WriteLine($"{DateTime.Now:HH:mm:ss.fff}  FEHLER   {error}"));
    Console.ReadLine();
    Console.WriteLine($"Beendet nach {(DateTime.Now - start).TotalSeconds:0} s.");
    return 0;
}
