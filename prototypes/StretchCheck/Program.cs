using System.Diagnostics;
using System.Globalization;
using JustPractise.Prototypes.StretchCheck;
using SoundFlow.Backends.MiniAudio;
using SoundFlow.Backends.MiniAudio.Devices;
using SoundFlow.Backends.MiniAudio.Enums;
using SoundFlow.Components;
using SoundFlow.Enums;
using SoundFlow.Structs;

// Prüfpunkt 3: Time-Stretching mit signalsmith-stretch (nativ) und WSOLA (SoundFlow).
//
//   dotnet run -c Release --project prototypes/StretchCheck -- render <datei> [--start s] [--seconds s] [--out ordner]
//   dotnet run -c Release --project prototypes/StretchCheck -- play <datei> [--start s] [--seconds s] [--buffer ms]

var culture = CultureInfo.InvariantCulture;
if (args.Length < 2 || args[0] is not ("render" or "play"))
{
    Console.WriteLine("Aufruf: render <datei> [--start s] [--seconds s] [--out ordner]");
    Console.WriteLine("        play <datei> [--start s] [--seconds s] [--buffer ms]");
    return 1;
}

var mode = args[0];
var file = args[1];
double start = 0;
double? seconds = mode == "render" ? 30 : null;
uint bufferMs = 10;
var outDir = "stretch-out";
for (var i = 2; i + 1 < args.Length; i += 2)
{
    switch (args[i])
    {
        case "--start":
            start = double.Parse(args[i + 1], culture);
            break;
        case "--seconds":
            seconds = double.Parse(args[i + 1], culture);
            break;
        case "--buffer":
            bufferMs = uint.Parse(args[i + 1], culture);
            break;
        case "--out":
            outDir = args[i + 1];
            break;
        default:
            Console.WriteLine($"Unbekannte Option {args[i]}");
            return 1;
    }
}

using var engine = new MiniAudioEngine();
var watch = Stopwatch.StartNew();
var clip = AudioClip.Load(engine, file, start, seconds);
Console.WriteLine($"{Path.GetFileName(file)}: {clip.SampleRate} Hz, {clip.Channels} Kanäle, " +
    $"{clip.Seconds:0.0} s ab {start:0.0} s, decodiert in {watch.ElapsedMilliseconds} ms");

if (mode == "render")
{
    Render(engine, clip, outDir);
}
else
{
    Play(engine, clip, bufferMs);
}

return 0;

static IStretchVoice[] CreateVoices(AudioClip clip) =>
[
    new SignalsmithVoice("signalsmith", clip.Channels, clip.SampleRate, 0),
    new SignalsmithVoice("signalsmith-split", clip.Channels, clip.SampleRate, NativeMethods.OptionSplit),
    new SignalsmithVoice("signalsmith-cheaper", clip.Channels, clip.SampleRate, NativeMethods.OptionCheaper | NativeMethods.OptionSplit),
    new WsolaVoice("wsola-balanced", clip.Channels, WsolaPerformancePreset.Balanced),
    new WsolaVoice("wsola-highquality", clip.Channels, WsolaPerformancePreset.HighQuality),
];

static void Render(MiniAudioEngine engine, AudioClip clip, string outDir)
{
    Directory.CreateDirectory(outDir);
    clip.WriteWav(engine, Path.Combine(outDir, "original.wav"));

    // Blöcke wie im Audio-Callback bei 10 ms Puffer
    var blockFrames = clip.SampleRate / 100;
    double[] tempos = [0.5, 0.75, 1.5];

    Console.WriteLine();
    Console.WriteLine($"{"Verfahren",-20} {"Tempo",6} {"Echtzeit",10} {"Ø Block",9} {"max Block",10} {"Latenz",8}");
    foreach (var tempo in tempos)
    {
        var voices = CreateVoices(clip);
        try
        {
            foreach (var voice in voices)
            {
                var stats = new ProcessStats(clip.SampleRate);
                var outFrames = (int)(clip.Frames / tempo);
                var output = new float[outFrames * clip.Channels];

                // Aufwärmen, damit JIT und erste Berechnungen nicht in die Messung fallen
                voice.Start(clip, 0, tempo);
                voice.Render(clip, output.AsSpan(0, Math.Min(output.Length, clip.SampleRate * clip.Channels)), tempo, loop: false);

                voice.Start(clip, 0, tempo);
                for (var frame = 0; frame < outFrames; frame += blockFrames)
                {
                    var frames = Math.Min(blockFrames, outFrames - frame);
                    var block = output.AsSpan(frame * clip.Channels, frames * clip.Channels);
                    var t0 = Stopwatch.GetTimestamp();
                    voice.Render(clip, block, tempo, loop: false);
                    stats.Record(Stopwatch.GetTimestamp() - t0, frames);
                }

                var average = stats.TotalSeconds * 1000.0 / Math.Ceiling((double)outFrames / blockFrames);
                Console.WriteLine($"{voice.Name,-20} {tempo * 100,5:0}% {stats.RealtimeFactor,9:0}x " +
                    $"{average,6:0.00} ms {stats.MaxMilliseconds,7:0.00} ms {voice.LatencyFrames * 1000.0 / clip.SampleRate,5:0} ms");

                if (voice.Name != "signalsmith-split")
                {
                    var name = $"{voice.Name}-{tempo * 100:0}.wav";
                    AudioClip.WriteWav(engine, Path.Combine(outDir, name), output, clip.Channels, clip.SampleRate);
                }
            }
        }
        finally
        {
            foreach (var voice in voices)
            {
                voice.Dispose();
            }
        }
    }

    Console.WriteLine();
    Console.WriteLine($"Budget pro Block: {blockFrames * 1000.0 / clip.SampleRate:0.0} ms. WAV-Dateien in {Path.GetFullPath(outDir)}");
}

static void Play(MiniAudioEngine engine, AudioClip clip, uint bufferMs)
{
    var format = new AudioFormat
    {
        Format = SampleFormat.F32,
        SampleRate = clip.SampleRate,
        Channels = clip.Channels,
        Layout = AudioFormat.GetLayoutFromChannels(clip.Channels),
    };
    var config = new MiniAudioDeviceConfig
    {
        PeriodSizeInMilliseconds = bufferMs,
        Periods = 2,
        Playback = new DeviceSubConfig { ShareMode = ShareMode.Shared },
    };

    var voices = CreateVoices(clip);
    using var device = engine.InitializePlaybackDevice(null, format, config);
    var player = new StretchPlayer(engine, format, clip, voices);
    device.MasterMixer.AddComponent(player);
    device.Start();

    Console.WriteLine($"Ausgabe: {device.Info?.Name ?? "Standard"}, Puffer {bufferMs} ms");
    PrintHelp(voices);
    player.Playing = true;

    var lastStatus = 0L;
    while (true)
    {
        if (Console.KeyAvailable)
        {
            var key = Console.ReadKey(intercept: true);
            Console.WriteLine();
            if (key.Key is ConsoleKey.Q or ConsoleKey.Escape)
            {
                break;
            }

            HandleKey(player, key);
        }

        if (Stopwatch.GetElapsedTime(lastStatus).TotalMilliseconds >= 100)
        {
            lastStatus = Stopwatch.GetTimestamp();
            var line = $"{(player.Playing ? "Playing" : "Paused ")} {player.Seconds,6:0.0}/{clip.Seconds:0.0} s | " +
                $"{player.Voice.Name} {player.Tempo * 100:0}% | {player.Stats}";
            var width = Math.Max(20, Console.WindowWidth - 1);
            Console.Write("\r" + (line.Length > width ? line[..width] : line.PadRight(width)));
        }

        Thread.Sleep(10);
    }

    device.Stop();
    device.MasterMixer.RemoveComponent(player);
    foreach (var voice in voices)
    {
        voice.Dispose();
    }

    static void HandleKey(StretchPlayer player, ConsoleKeyInfo key)
    {
        switch (key.Key)
        {
            case ConsoleKey.Spacebar:
                player.Playing = !player.Playing;
                break;
            case ConsoleKey.LeftArrow:
                player.SeekTo(player.Seconds - 5);
                break;
            case ConsoleKey.RightArrow:
                player.SeekTo(player.Seconds + 5);
                break;
            case ConsoleKey.UpArrow:
                player.Tempo += 0.05f;
                break;
            case ConsoleKey.DownArrow:
                player.Tempo -= 0.05f;
                break;
            case >= ConsoleKey.D1 and <= ConsoleKey.D9:
                player.SelectVoice(key.Key - ConsoleKey.D1);
                player.Stats.Reset();
                break;
            case ConsoleKey.R:
                player.Stats.Reset();
                break;
            default:
                PrintHelp(player.Voices);
                break;
        }
    }

    static void PrintHelp(IReadOnlyList<IStretchVoice> voices)
    {
        var list = string.Join(", ", voices.Select((v, i) => $"{i + 1} {v.Name}"));
        Console.WriteLine($"Leertaste Play/Pause, ←/→ ±5 s, ↑/↓ Tempo ±5 %, {list}, R Statistik zurücksetzen, Q Ende");
    }
}
