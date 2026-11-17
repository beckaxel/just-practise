using System.Diagnostics;
using System.Runtime.InteropServices;

namespace JustPractise.Prototypes.DecodeCheck;

/// <summary>Ergebnis eines Lastlaufs mit einem großen Audiopuffer im Speicher.</summary>
internal sealed record GcResult(
    string Name,
    double BufferMegabytes,
    double AllocateMilliseconds,
    double HeapMegabytes,
    double WorkingSetMegabytes,
    int Gen0,
    int Gen1,
    int Gen2,
    int FullBlocking,
    double MaxEphemeral,
    double MaxBackground,
    double MaxFullBlocking,
    double TotalPause);

/// <summary>
/// Hält einen Puffer in der Größe eines 15-minütigen Stücks (48 kHz, Stereo) und erzeugt dabei
/// UI-artige Last wie in Prüfpunkt 1. Gemessen wird, ob der große Puffer die GC-Pausen verlängert
/// und was beim Wechsel zwischen Dateien passiert, wenn ein neuer großer Puffer entsteht.
/// </summary>
internal sealed class GcScenarios(double seconds)
{
    public const int Minutes = 15;
    private const int Rate = 48_000;
    private const int Channels = 2;
    private const long Samples = (long)Minutes * 60 * Rate * Channels;

    // Last wie eine geschäftige Oberfläche, mit dauerhaft lebenden Objekten, damit auch volle GCs anfallen
    private const double LoadMegabytesPerSecond = 100;
    private const int RetainedObjects = 500_000;

    private readonly LoadGenerator _load = new() { MegabytesPerSecond = LoadMegabytesPerSecond };

    public IEnumerable<GcResult> Run()
    {
        _load.SetRetainedObjects(RetainedObjects);

        // Die ersten Sekunden befördern das Objektgeflecht in Generation 2, das gehört nicht zur Messung
        Measure("Aufwärmen", 0, () => 0, null, Math.Min(seconds, 5));

        yield return Measure("ohne Puffer", 0, () => 0, null);

        float[]? floats = null;
        var ms = Allocate(() => floats = Filled<float>(Samples));
        yield return Measure($"float[] {Minutes} min", Samples * sizeof(float) / 1e6, () => ms, null);
        GC.KeepAlive(floats);
        floats = null;

        short[]? shorts = null;
        ms = Allocate(() => shorts = Filled<short>(Samples));
        yield return Measure($"short[] {Minutes} min", Samples * sizeof(short) / 1e6, () => ms, null);
        GC.KeepAlive(shorts);
        shorts = null;

        yield return MeasureNative();

        // Dateiwechsel: alle 2 s ein neuer Puffer, der alte wird zu Müll. Bis der neue fertig ist, leben beide.
        float[]? current = Filled<float>(Samples);
        var maxAllocate = 0.0;
        var nextSwitch = 2.0;
        yield return Measure($"Dateiwechsel alle 2 s, float[] {Minutes} min", Samples * sizeof(float) / 1e6, () => maxAllocate, elapsed =>
        {
            if (elapsed < nextSwitch)
            {
                return;
            }

            nextSwitch += 2;
            maxAllocate = Math.Max(maxAllocate, Allocate(() => current = Filled<float>(Samples)));
        });
        GC.KeepAlive(current);
        current = null;

        // Dateiwechsel mit einem einzigen Puffer, der nur neu beschrieben wird
        var reused = Filled<float>(Samples);
        maxAllocate = 0;
        nextSwitch = 2.0;
        yield return Measure($"Dateiwechsel alle 2 s, ein float[] wiederverwendet", Samples * sizeof(float) / 1e6, () => maxAllocate, elapsed =>
        {
            if (elapsed < nextSwitch)
            {
                return;
            }

            nextSwitch += 2;
            maxAllocate = Math.Max(maxAllocate, Allocate(() => reused.AsSpan().Fill(0.25f)));
        });
        GC.KeepAlive(reused);
        reused = null;

        // Dateiwechsel mit Blöcken aus dem Pool: alte Blöcke zurückgeben, neue holen und beschreiben
        var pool = new BlockPool();
        var audio = new BlockAudio(pool, Channels);
        FillBlocks(audio);
        maxAllocate = 0;
        nextSwitch = 2.0;
        yield return Measure($"Dateiwechsel alle 2 s, Blöcke aus Pool", Samples * sizeof(float) / 1e6, () => maxAllocate, elapsed =>
        {
            if (elapsed < nextSwitch)
            {
                return;
            }

            nextSwitch += 2;
            maxAllocate = Math.Max(maxAllocate, Allocate(() =>
            {
                audio.Release();
                FillBlocks(audio);
            }));
        });
        Console.WriteLine($"  Pool: {pool.Created} Blöcke angelegt");
        GC.KeepAlive(audio);
    }

    private unsafe GcResult MeasureNative()
    {
        var bytes = (nuint)(Samples * sizeof(float));
        float* pointer = null;
        var ms = Allocate(() =>
        {
            pointer = (float*)NativeMemory.Alloc(bytes);
            new Span<float>(pointer, (int)Samples).Fill(0.25f);
        });
        GC.AddMemoryPressure((long)bytes);
        try
        {
            return Measure($"nativ {Minutes} min (NativeMemory)", bytes / 1e6, () => ms, null);
        }
        finally
        {
            GC.RemoveMemoryPressure((long)bytes);
            NativeMemory.Free(pointer);
        }
    }

    private GcResult Measure(string name, double bufferMb, Func<double> allocateMs, Action<double>? onTick, double? duration = null)
    {
        var limit = duration ?? seconds;
        Console.WriteLine($"  {name} ...");
        var monitor = new GcMonitor();
        var start = Stopwatch.GetTimestamp();
        var last = start;
        while (true)
        {
            var now = Stopwatch.GetTimestamp();
            var elapsed = Stopwatch.GetElapsedTime(start, now).TotalSeconds;
            if (elapsed >= limit)
            {
                break;
            }

            _load.Tick(Stopwatch.GetElapsedTime(last, now));
            last = now;
            onTick?.Invoke(elapsed);
            monitor.Poll();
            Thread.Sleep(16);
        }

        monitor.Poll();
        return new GcResult(
            name,
            bufferMb,
            allocateMs(),
            GcMonitor.HeapMegabytes,
            Environment.WorkingSet / 1e6,
            monitor.Count(0),
            monitor.Count(1),
            monitor.Count(2),
            monitor.FullBlockingSeen,
            monitor.MaxEphemeralPause,
            monitor.MaxBackgroundPause,
            monitor.MaxFullBlockingPause,
            monitor.TotalPauseMilliseconds);
    }

    private static void FillBlocks(BlockAudio audio)
    {
        var frames = Samples / Channels;
        while (audio.Frames < frames)
        {
            var tail = audio.WritableTail((int)Math.Min(int.MaxValue, (frames - audio.Frames) * Channels));
            tail.Fill(0.25f);
            audio.Commit(tail.Length);
        }
    }

    /// <summary>Legt ein Array an und beschreibt es ganz, damit das Betriebssystem den Speicher wirklich hergibt.</summary>
    private static T[] Filled<T>(long length)
        where T : struct
    {
        var array = GC.AllocateUninitializedArray<T>((int)length);
        array.AsSpan().Clear();
        return array;
    }

    private static double Allocate(Action action)
    {
        var start = Stopwatch.GetTimestamp();
        action();
        return Stopwatch.GetElapsedTime(start).TotalMilliseconds;
    }
}
