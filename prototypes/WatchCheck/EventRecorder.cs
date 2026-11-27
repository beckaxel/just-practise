using System.Collections.Concurrent;
using System.Diagnostics;

namespace JustPractise.Prototypes.WatchCheck;

internal sealed record WatchEvent(TimeSpan Time, WatcherChangeTypes Kind, string Path, string? OldPath);

/// <summary>Überwacht einen Ordner rekursiv und sammelt alle Ereignisse mit Zeitpunkt, auch Pufferüberläufe.</summary>
internal sealed class EventRecorder : IDisposable
{
    private readonly FileSystemWatcher watcher;
    private readonly ConcurrentQueue<WatchEvent> events = new();
    private readonly ConcurrentQueue<string> errors = new();
    private readonly Stopwatch clock = Stopwatch.StartNew();
    private long lastEventTicks;

    public EventRecorder(string path, Action<WatchEvent>? onEvent = null, Action<string>? onError = null)
    {
        watcher = new FileSystemWatcher(path)
        {
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite | NotifyFilters.Size,
        };
        void Add(WatchEvent watchEvent)
        {
            events.Enqueue(watchEvent);
            Interlocked.Exchange(ref lastEventTicks, clock.Elapsed.Ticks);
            onEvent?.Invoke(watchEvent);
        }

        watcher.Created += (_, e) => Add(new WatchEvent(clock.Elapsed, e.ChangeType, e.FullPath, null));
        watcher.Deleted += (_, e) => Add(new WatchEvent(clock.Elapsed, e.ChangeType, e.FullPath, null));
        watcher.Changed += (_, e) => Add(new WatchEvent(clock.Elapsed, e.ChangeType, e.FullPath, null));
        watcher.Renamed += (_, e) => Add(new WatchEvent(clock.Elapsed, e.ChangeType, e.FullPath, e.OldFullPath));
        watcher.Error += (_, e) =>
        {
            var message = e.GetException().GetType().Name + ": " + e.GetException().Message;
            errors.Enqueue(message);
            onError?.Invoke(message);
        };
        watcher.EnableRaisingEvents = true;
    }

    public TimeSpan Now => clock.Elapsed;

    public int InternalBufferSize => watcher.InternalBufferSize;

    /// <summary>Nimmt alle bisher gesammelten Ereignisse und Fehler heraus.</summary>
    public (List<WatchEvent> Events, List<string> Errors) Drain()
    {
        var drained = new List<WatchEvent>();
        while (events.TryDequeue(out var watchEvent))
        {
            drained.Add(watchEvent);
        }

        var drainedErrors = new List<string>();
        while (errors.TryDequeue(out var error))
        {
            drainedErrors.Add(error);
        }

        return (drained, drainedErrors);
    }

    /// <summary>Wartet, bis <paramref name="quiet"/> lang nichts mehr kam, höchstens <paramref name="max"/>.</summary>
    public void WaitForQuiet(TimeSpan quiet, TimeSpan max)
    {
        var start = clock.Elapsed;
        while (clock.Elapsed - start < max)
        {
            Thread.Sleep(50);
            var last = TimeSpan.FromTicks(Interlocked.Read(ref lastEventTicks));
            if (clock.Elapsed - Max(last, start) >= quiet)
            {
                return;
            }
        }
    }

    public void Dispose() => watcher.Dispose();

    private static TimeSpan Max(TimeSpan a, TimeSpan b) => a > b ? a : b;
}
