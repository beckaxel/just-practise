using System.Runtime;

namespace JustPractise.Prototypes.DecodeCheck;

/// <summary>
/// Fragt regelmäßig ab, welche Speicherbereinigungen gelaufen sind und wie lange sie die Threads angehalten haben.
/// Zwischen zwei Abfragen kann mehr als ein GC einer Art liegen, dann zählt nur der letzte zur längsten Pause.
/// </summary>
internal sealed class GcMonitor
{
    private static readonly GCKind[] Kinds = [GCKind.Ephemeral, GCKind.Background, GCKind.FullBlocking];

    private readonly long[] _lastIndex = new long[Kinds.Length];
    private readonly double[] _maxPause = new double[Kinds.Length];
    private readonly int[] _baseCounts = new int[3];
    private TimeSpan _basePause;
    private int _fullBlocking;

    public GcMonitor() => Reset();

    public static string Mode =>
        $"{(GCSettings.IsServerGC ? "Server" : "Workstation")}-GC, " +
        $"{(IsConcurrent ? "mit" : "ohne")} Hintergrund-GC";

    /// <summary>Hintergrund-GC lässt sich per runtimeconfig oder Umgebungsvariable DOTNET_gcConcurrent=0 abschalten.</summary>
    private static bool IsConcurrent =>
        Environment.GetEnvironmentVariable("DOTNET_gcConcurrent") != "0"
        && !(AppContext.TryGetSwitch("System.GC.Concurrent", out var concurrent) && !concurrent);

    /// <summary>Anzahl der GCs pro Generation seit dem Zurücksetzen.</summary>
    public int Count(int generation) => GC.CollectionCount(generation) - _baseCounts[generation];

    public double TotalPauseMilliseconds => (GC.GetTotalPauseDuration() - _basePause).TotalMilliseconds;

    public double MaxEphemeralPause => _maxPause[0];

    public double MaxBackgroundPause => _maxPause[1];

    public double MaxFullBlockingPause => _maxPause[2];

    /// <summary>Gesehene blockierende volle GCs (zwischen zwei Abfragen höchstens einer).</summary>
    public int FullBlockingSeen => _fullBlocking;

    public static double HeapMegabytes => GC.GetGCMemoryInfo().HeapSizeBytes / 1e6;

    /// <summary>Liest die zuletzt gelaufenen GCs und liefert die längste neue Pause in ms (0, wenn keiner lief).</summary>
    public double Poll()
    {
        var longest = 0.0;
        for (var i = 0; i < Kinds.Length; i++)
        {
            var info = GC.GetGCMemoryInfo(Kinds[i]);
            if (info.Index == _lastIndex[i])
            {
                continue;
            }

            _lastIndex[i] = info.Index;
            if (Kinds[i] == GCKind.FullBlocking)
            {
                _fullBlocking++;
            }

            var pause = info.PauseDurations.Length > 0 ? info.PauseDurations[0].TotalMilliseconds : 0;
            if (info.PauseDurations.Length > 1)
            {
                pause = Math.Max(pause, info.PauseDurations[1].TotalMilliseconds);
            }

            _maxPause[i] = Math.Max(_maxPause[i], pause);
            longest = Math.Max(longest, pause);
        }

        return longest;
    }

    public void Reset()
    {
        for (var i = 0; i < Kinds.Length; i++)
        {
            _lastIndex[i] = GC.GetGCMemoryInfo(Kinds[i]).Index;
            _maxPause[i] = 0;
        }

        for (var generation = 0; generation < 3; generation++)
        {
            _baseCounts[generation] = GC.CollectionCount(generation);
        }

        _basePause = GC.GetTotalPauseDuration();
        _fullBlocking = 0;
    }
}
