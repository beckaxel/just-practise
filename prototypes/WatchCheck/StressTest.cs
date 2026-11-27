using System.Globalization;

namespace JustPractise.Prototypes.WatchCheck;

/// <summary>
/// Erzeugt in einem überwachten Testordner typische Änderungen in Bursts und prüft, welche Ereignisse ankommen.
/// Jeder Schritt arbeitet in einem eigenen Unterordner und wartet danach, bis 500 ms lang nichts mehr kam.
/// </summary>
internal sealed class StressTest(string root, Action<string> line)
{
    private static readonly byte[] Content = new byte[4096];

    private readonly string outside = root.TrimEnd(Path.DirectorySeparatorChar) + "-outside";
    private int step;

    public int Run()
    {
        Directory.CreateDirectory(root);
        Directory.CreateDirectory(outside);
        var problems = 0;
        try
        {
            using var recorder = new EventRecorder(root);
            line($"Testordner {root}, InternalBufferSize {recorder.InternalBufferSize} Bytes");
            line("");
            line("| Schritt | erwartet | angekommen | Created | Changed | Renamed | Deleted | Fehler | Dauer bis letztes Ereignis |");
            line("|---|---|---|---|---|---|---|---|---|");

            problems += Step(recorder, "1000 Dateien anlegen", CreateFlat, WatcherChangeTypes.Created);
            problems += Step(recorder, "20 neue Ordner, sofort je 20 Dateien darin", CreateFoldersWithFiles, WatcherChangeTypes.Created);
            problems += Step(recorder, "Baum mit 10 Ebenen, je 5 Dateien", CreateDeepTree, WatcherChangeTypes.Created);
            problems += Step(recorder, "500 Dateien umbenennen", RenameFiles, WatcherChangeTypes.Renamed);
            problems += Step(recorder, "200 Dateien ändern (anhängen)", AppendFiles, WatcherChangeTypes.Changed);
            problems += Step(recorder, "Ordner mit 200 Dateien von außen hineinschieben", MoveFolderIn, WatcherChangeTypes.Created);
            problems += Step(recorder, "Ordner mit 100 Dateien umbenennen", RenameFolder, WatcherChangeTypes.Renamed);
            problems += Step(recorder, "danach Dateien im hineingeschobenen Ordner und seinem Unterordner anlegen", CreateInMovedFolder, WatcherChangeTypes.Created);
            problems += Step(recorder, "danach Dateien im umbenannten Ordner und seinem Unterordner anlegen", CreateInRenamedFolder, WatcherChangeTypes.Created);
            problems += Step(recorder, "100× atomar speichern (tmp schreiben, über Ziel umbenennen)", AtomicSave, WatcherChangeTypes.Renamed);
            problems += Step(recorder, "Ordner mit 1000 Dateien löschen", DeleteFolder, WatcherChangeTypes.Deleted);
            problems += Step(recorder, "Burst: 10 000 Dateien so schnell wie möglich", Burst, WatcherChangeTypes.Created);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
            Directory.Delete(outside, recursive: true);
        }

        return problems;
    }

    /// <param name="prepare">bereitet im Ordner vor und liefert den eigentlichen Schritt; der gibt die erwarteten Pfade zurück</param>
    private int Step(EventRecorder recorder, string name, Func<string, Func<IReadOnlyCollection<string>>> prepare, WatcherChangeTypes expectedKind)
    {
        var folder = Path.Combine(root, $"step-{++step:00}");
        Directory.CreateDirectory(folder);
        var action = prepare(folder);
        Thread.Sleep(300);
        recorder.WaitForQuiet(TimeSpan.FromMilliseconds(500), TimeSpan.FromSeconds(10));
        recorder.Drain();

        var start = recorder.Now;
        var expected = action();
        recorder.WaitForQuiet(TimeSpan.FromMilliseconds(500), TimeSpan.FromSeconds(20));
        var (events, errors) = recorder.Drain();

        var arrived = events.Where(e => e.Kind == expectedKind).Select(e => e.Path).ToHashSet(StringComparer.Ordinal);
        var matched = expected.Count(arrived.Contains);
        var last = events.Count > 0 ? events.Max(e => e.Time) - start : TimeSpan.Zero;
        int Count(WatcherChangeTypes kind) => events.Count(e => e.Kind == kind);
        line(string.Create(CultureInfo.InvariantCulture,
            $"| {name} | {expected.Count} {expectedKind} | {matched} | {Count(WatcherChangeTypes.Created)} | {Count(WatcherChangeTypes.Changed)} | {Count(WatcherChangeTypes.Renamed)} | {Count(WatcherChangeTypes.Deleted)} | {(errors.Count > 0 ? string.Join("; ", errors.Distinct()) : "0")} | {last.TotalMilliseconds:0} ms |"));
        return matched == expected.Count && errors.Count == 0 ? 0 : 1;
    }

    private static List<string> CreateFiles(string folder, int count, string prefix = "file")
    {
        var paths = new List<string>(count);
        for (var i = 0; i < count; i++)
        {
            var path = Path.Combine(folder, $"{prefix}-{i:00000}.mp3");
            File.WriteAllBytes(path, Content);
            paths.Add(path);
        }

        return paths;
    }

    private static Func<IReadOnlyCollection<string>> CreateFlat(string folder) => () => CreateFiles(folder, 1000);

    private static Func<IReadOnlyCollection<string>> CreateFoldersWithFiles(string folder) => () =>
    {
        var paths = new List<string>();
        for (var i = 0; i < 20; i++)
        {
            var sub = Path.Combine(folder, $"album-{i:00}");
            Directory.CreateDirectory(sub);
            paths.Add(sub);
            paths.AddRange(CreateFiles(sub, 20));
        }

        return paths;
    };

    private static Func<IReadOnlyCollection<string>> CreateDeepTree(string folder) => () =>
    {
        var paths = new List<string>();
        var current = folder;
        for (var level = 0; level < 10; level++)
        {
            current = Path.Combine(current, $"level-{level}");
            Directory.CreateDirectory(current);
            paths.Add(current);
            paths.AddRange(CreateFiles(current, 5));
        }

        return paths;
    };

    private static Func<IReadOnlyCollection<string>> RenameFiles(string folder)
    {
        var sources = CreateFiles(folder, 500);
        return () => sources.Select(source =>
        {
            var target = source.Replace("file-", "renamed-", StringComparison.Ordinal);
            File.Move(source, target);
            return target;
        }).ToList();
    }

    private static Func<IReadOnlyCollection<string>> AppendFiles(string folder)
    {
        var paths = CreateFiles(folder, 200);
        return () =>
        {
            foreach (var path in paths)
            {
                using var stream = new FileStream(path, FileMode.Append);
                stream.Write(Content);
            }

            return paths;
        };
    }

    // Erwartet wird nur der Ordner selbst; ob auch die Dateien darin gemeldet werden, zeigt die Spalte Created.
    private Func<IReadOnlyCollection<string>> MoveFolderIn(string folder)
    {
        var source = Path.Combine(outside, "album");
        Directory.CreateDirectory(source);
        CreateFiles(source, 200);
        var target = Path.Combine(folder, "album");
        return () =>
        {
            Directory.Move(source, target);
            return [target];
        };
    }

    private static Func<IReadOnlyCollection<string>> RenameFolder(string folder)
    {
        var source = Path.Combine(folder, "album");
        Directory.CreateDirectory(source);
        CreateFiles(source, 100);
        var target = Path.Combine(folder, "album-renamed");
        return () =>
        {
            Directory.Move(source, target);
            return [target];
        };
    }

    // Ein Ordner mit Unterordner wird von außen hineingeschoben; erst danach (im Schritt) entstehen darin Dateien.
    private Func<IReadOnlyCollection<string>> CreateInMovedFolder(string folder)
    {
        var source = Path.Combine(outside, "moved");
        Directory.CreateDirectory(Path.Combine(source, "cd1"));
        var target = Path.Combine(folder, "moved");
        Directory.Move(source, target);
        return () => [.. CreateFiles(target, 10), .. CreateFiles(Path.Combine(target, "cd1"), 10)];
    }

    // Ein überwachter Ordner mit Unterordner wird umbenannt; erst danach (im Schritt) entstehen darin Dateien.
    private static Func<IReadOnlyCollection<string>> CreateInRenamedFolder(string folder)
    {
        var source = Path.Combine(folder, "before");
        Directory.CreateDirectory(Path.Combine(source, "cd1"));
        Thread.Sleep(500);
        var target = Path.Combine(folder, "after");
        Directory.Move(source, target);
        return () => [.. CreateFiles(target, 10), .. CreateFiles(Path.Combine(target, "cd1"), 10)];
    }

    private static Func<IReadOnlyCollection<string>> AtomicSave(string folder)
    {
        var targets = CreateFiles(folder, 100, "piece");
        return () =>
        {
            foreach (var target in targets)
            {
                var temp = target + ".tmp";
                File.WriteAllBytes(temp, Content);
                File.Move(temp, target, overwrite: true);
            }

            return targets;
        };
    }

    private static Func<IReadOnlyCollection<string>> DeleteFolder(string folder)
    {
        var sub = Path.Combine(folder, "album");
        Directory.CreateDirectory(sub);
        var paths = CreateFiles(sub, 1000);
        paths.Add(sub);
        return () =>
        {
            Directory.Delete(sub, recursive: true);
            return paths;
        };
    }

    private static Func<IReadOnlyCollection<string>> Burst(string folder) => () => CreateFiles(folder, 10_000);
}
