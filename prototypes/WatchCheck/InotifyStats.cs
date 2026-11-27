namespace JustPractise.Prototypes.WatchCheck;

/// <summary>Grenzen und Verbrauch von inotify unter Linux (aus /proc). Unter anderen Systemen leer.</summary>
internal sealed record InotifyStats(long MaxWatches, long MaxInstances, long MaxQueuedEvents, int OwnInstances, int OwnWatches, int UserInstances, int UserWatches)
{
    public static InotifyStats? Read()
    {
        if (!OperatingSystem.IsLinux())
        {
            return null;
        }

        var (ownInstances, ownWatches) = Count("/proc/self");
        int userInstances = 0, userWatches = 0;
        foreach (var process in Directory.EnumerateDirectories("/proc"))
        {
            if (!int.TryParse(Path.GetFileName(process), out _))
            {
                continue;
            }

            var (instances, watches) = Count(process);
            userInstances += instances;
            userWatches += watches;
        }

        return new InotifyStats(Limit("max_user_watches"), Limit("max_user_instances"), Limit("max_queued_events"),
            ownInstances, ownWatches, userInstances, userWatches);
    }

    public override string ToString() =>
        $"inotify: dieser Prozess {OwnInstances} Instanzen / {OwnWatches} Watches; alle eigenen Prozesse {UserInstances}/{MaxInstances} Instanzen, " +
        $"{UserWatches}/{MaxWatches} Watches; Warteschlange {MaxQueuedEvents} Ereignisse";

    private static long Limit(string name) => long.Parse(File.ReadAllText($"/proc/sys/fs/inotify/{name}").Trim(), System.Globalization.CultureInfo.InvariantCulture);

    // Zählt die inotify-Dateideskriptoren eines Prozesses und ihre Watches (Zeilen "inotify wd:" in fdinfo).
    private static (int Instances, int Watches) Count(string process)
    {
        int instances = 0, watches = 0;
        try
        {
            foreach (var fd in Directory.EnumerateFiles(Path.Combine(process, "fd")))
            {
                string? target;
                try
                {
                    target = new FileInfo(fd).LinkTarget;
                }
                catch (IOException)
                {
                    continue;
                }

                if (target != "anon_inode:inotify")
                {
                    continue;
                }

                instances++;
                try
                {
                    watches += File.ReadLines(Path.Combine(process, "fdinfo", Path.GetFileName(fd)))
                        .Count(line => line.StartsWith("inotify wd:", StringComparison.Ordinal));
                }
                catch (IOException)
                {
                }
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Prozesse anderer Benutzer oder inzwischen beendet
        }

        return (instances, watches);
    }
}
