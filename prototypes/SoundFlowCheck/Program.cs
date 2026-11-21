using System.Diagnostics;
using JustPractise.Prototypes.SoundFlowCheck;
using SoundFlow.Backends.MiniAudio;
using SoundFlow.Enums;
using SoundFlow.Providers;
using SoundFlow.Structs;

// Prüfpunkt 2: SoundFlow als Audio-Engine.
//
//   dotnet run --project prototypes/SoundFlowCheck -- --list
//   dotnet run --project prototypes/SoundFlowCheck -- --to-wav <eingabe> <ausgabe.wav>
//   dotnet run --project prototypes/SoundFlowCheck -- [--buffer <ms>] [<datei>]

if (args is ["--list"])
{
    using var engine = new MiniAudioEngine();
    PrintDevices(engine);
    return 0;
}

if (args is ["--to-wav", var source, var target])
{
    ConvertToWav(source, target);
    return 0;
}

uint bufferMs = 10;
string? file = null;
for (var i = 0; i < args.Length; i++)
{
    if (args[i] == "--buffer" && i + 1 < args.Length)
    {
        bufferMs = uint.Parse(args[++i], System.Globalization.CultureInfo.InvariantCulture);
    }
    else
    {
        file = args[i];
    }
}

using var session = new Session(null, bufferMs);
Console.WriteLine($"Backend: {session.Engine.ActiveBackend}, Ausgabe: {session.OutputName}, Puffer: {bufferMs} ms");
if (file is not null)
{
    Console.WriteLine(session.Load(file));
}

PrintHelp();
RunLoop(session);
return 0;

static void RunLoop(Session session)
{
    var lastStatus = 0L;
    while (true)
    {
        if (Console.KeyAvailable)
        {
            var key = Console.ReadKey(intercept: true);
            Console.WriteLine();
            if (key.Key is ConsoleKey.Q or ConsoleKey.Escape)
            {
                return;
            }

            HandleKey(session, key);
        }

        if (Stopwatch.GetElapsedTime(lastStatus).TotalMilliseconds >= 100)
        {
            lastStatus = Stopwatch.GetTimestamp();
            WriteStatus(session);
        }

        Thread.Sleep(10);
    }
}

static void HandleKey(Session session, ConsoleKeyInfo key)
{
    var player = session.Player;
    switch (key.Key)
    {
        case ConsoleKey.Spacebar when player is not null:
            if (player.State == PlaybackState.Playing)
            {
                player.Pause();
            }
            else
            {
                player.Play();
            }

            break;
        case ConsoleKey.LeftArrow when player is not null:
            player.Seek(TimeSpan.FromSeconds(Math.Max(0, player.Time - 5)), SeekOrigin.Begin);
            break;
        case ConsoleKey.RightArrow when player is not null:
            player.Seek(TimeSpan.FromSeconds(Math.Min(player.Duration - 0.1, player.Time + 5)), SeekOrigin.Begin);
            break;
        case ConsoleKey.UpArrow when player is not null:
            player.Volume = Math.Min(2f, player.Volume + 0.1f);
            break;
        case ConsoleKey.DownArrow when player is not null:
            player.Volume = Math.Max(0f, player.Volume - 0.1f);
            break;
        case ConsoleKey.F:
            Console.Write("Datei: ");
            var path = Console.ReadLine()?.Trim().Trim('"', '\'');
            if (!string.IsNullOrEmpty(path))
            {
                Console.WriteLine(TryRun(() => session.Load(path)));
            }

            break;
        case ConsoleKey.M:
            session.Click.Running = !session.Click.Running;
            break;
        case ConsoleKey.OemPlus or ConsoleKey.Add:
            session.Click.Bpm += 10;
            break;
        case ConsoleKey.OemMinus or ConsoleKey.Subtract:
            session.Click.Bpm -= 10;
            break;
        case ConsoleKey.O:
            ChooseOutput(session);
            break;
        case ConsoleKey.I:
            ChooseInput(session);
            break;
        case ConsoleKey.L:
            MeasureLatency(session);
            break;
        case ConsoleKey.R:
            session.Stats.Reset();
            break;
        default:
            PrintHelp();
            break;
    }
}

static void WriteStatus(Session session)
{
    var player = session.Player;
    var playback = player is null
        ? "keine Datei"
        : $"{player.State} {player.Time,6:0.0}/{player.Duration:0.0} s Vol {player.Volume:0.0}";
    var click = session.Click.Running ? $"Klick {session.Click.Bpm} bpm" : "Klick aus";
    var input = session.HasInput ? $"Ein {session.Meter.PeakDb,6:0.0} dB" : "Ein aus";
    var line = $"{playback} | {click} | {session.Stats} | {input}";
    var width = Math.Max(20, Console.WindowWidth - 1);
    Console.Write("\r" + (line.Length > width ? line[..width] : line.PadRight(width)));
}

static void ChooseOutput(Session session)
{
    var devices = session.Engine.PlaybackDevices;
    var index = Choose("Ausgabegerät", devices);
    if (index is null)
    {
        return;
    }

    Console.Write($"Puffer in ms [{session.BufferMs}]: ");
    var text = Console.ReadLine();
    var bufferMs = uint.TryParse(text, out var parsed) ? parsed : session.BufferMs;
    Console.WriteLine(TryRun(() =>
    {
        session.SwitchOutput(devices[index.Value], bufferMs);
        return $"Ausgabe: {session.OutputName}, Puffer {bufferMs} ms";
    }));
}

static void ChooseInput(Session session)
{
    var devices = session.Engine.CaptureDevices;
    Console.WriteLine("  -1: aus");
    var index = Choose("Eingabegerät", devices);
    if (index is null)
    {
        return;
    }

    Console.WriteLine(TryRun(() =>
    {
        if (index.Value < 0)
        {
            session.CloseInput();
        }
        else
        {
            session.OpenInput(devices[index.Value]);
        }

        return $"Eingabe: {session.InputName}";
    }));
}

static void MeasureLatency(Session session)
{
    if (!session.HasInput)
    {
        Console.WriteLine("Erst ein Eingabegerät wählen (I).");
        return;
    }

    var wasRunning = session.Click.Running;
    session.Click.Running = false;
    session.Player?.Pause();
    Thread.Sleep(500);

    var results = new List<double>();
    for (var i = 0; i < 8; i++)
    {
        var ms = session.MeasureRoundTrip();
        Console.WriteLine(ms is null ? "  kein Klick erkannt" : $"  {ms:0.0} ms");
        if (ms is not null)
        {
            results.Add(ms.Value);
        }

        Thread.Sleep(400);
    }

    if (results.Count > 0)
    {
        results.Sort();
        Console.WriteLine($"Rundlauf Median {results[results.Count / 2]:0.0} ms, min {results[0]:0.0}, max {results[^1]:0.0}");
    }

    session.Click.Running = wasRunning;
}

static int? Choose(string title, DeviceInfo[] devices)
{
    for (var i = 0; i < devices.Length; i++)
    {
        Console.WriteLine($"  {i}: {devices[i].Name}{(devices[i].IsDefault ? " (Standard)" : "")}");
    }

    Console.Write($"{title}: ");
    return int.TryParse(Console.ReadLine(), out var index) && index < devices.Length ? index : null;
}

static string TryRun(Func<string> action)
{
    try
    {
        return action();
    }
#pragma warning disable CA1031 // Im Prüfprogramm soll jeder Fehler sichtbar werden, ohne es zu beenden.
    catch (Exception ex)
#pragma warning restore CA1031
    {
        return $"Fehler: {ex.GetType().Name}: {ex.Message}";
    }
}

static void PrintDevices(MiniAudioEngine engine)
{
    Console.WriteLine($"Backend: {engine.ActiveBackend} (verfügbar: {string.Join(", ", MiniAudioEngine.AvailableBackends)})");
    Print("Ausgabegeräte", engine.PlaybackDevices);
    Print("Eingabegeräte", engine.CaptureDevices);

    static void Print(string title, DeviceInfo[] devices)
    {
        Console.WriteLine(title + ":");
        foreach (var device in devices)
        {
            var formats = string.Join(", ", device.SupportedDataFormats
                .Select(f => $"{f.Format}/{f.Channels}ch/{f.SampleRate}")
                .Distinct());
            Console.WriteLine($"  {device.Name}{(device.IsDefault ? " (Standard)" : "")}: {formats}");
        }
    }
}

static void ConvertToWav(string source, string target)
{
    using var engine = new MiniAudioEngine();
    using var input = File.OpenRead(source);
    using var provider = new StreamDataProvider(engine, input);
    var format = new AudioFormat
    {
        Format = SampleFormat.S16,
        SampleRate = provider.SampleRate,
        Channels = provider.FormatInfo?.ChannelCount ?? 2,
        Layout = AudioFormat.GetLayoutFromChannels(provider.FormatInfo?.ChannelCount ?? 2),
    };
    using var output = File.Create(target);
    using var encoder = engine.CreateEncoder(output, "wav", format);
    var buffer = new float[8192];
    int read;
    while ((read = provider.ReadBytes(buffer)) > 0)
    {
        encoder.Encode(buffer.AsSpan(0, read));
    }

    Console.WriteLine($"{target}: {format.SampleRate} Hz, {format.Channels} Kanäle");
}

static void PrintHelp() => Console.WriteLine(
    "Leertaste Play/Pause, ←/→ ±5 s, ↑/↓ Lautstärke, F Datei laden, M Metronom, +/- Tempo, " +
    "O Ausgabegerät, I Eingabegerät, L Latenz messen, R Statistik zurücksetzen, Q Ende");
