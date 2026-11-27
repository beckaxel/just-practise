using System.Diagnostics;
using System.Globalization;
using System.Text;
using JustPractise.Prototypes.AacCheck;
using SoundFlow.Backends.MiniAudio;
using SoundFlow.Metadata.Models;
using SoundFlow.Providers;

// Prüfpunkt 4: M4A/AAC über ein schlankes, mitgeliefertes FFmpeg. Gibt keinen Ton aus.
//
//   dotnet run -c Release --project prototypes/AacCheck -- <datei oder ordner>... [--wav ordner] [--out ordner]
//
// Ordner werden nach .m4a, .mp4, .aac und .m4b durchsucht. --wav schreibt jede Datei zusätzlich als WAV (float),
// zum Anhören in einem beliebigen Player. Liegt neben einer Datei eine MP3 derselben Aufnahme (<name>.mp3 oder
// <name>-converted.mp3), vergleicht es beide über die Lautstärke-Hüllkurve. Der Bericht landet in aaccheck-out/report.md.

var culture = CultureInfo.InvariantCulture;
var inputs = new List<string>();
string? wavDir = null;
var outDir = "aaccheck-out";
for (var i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--wav" when i + 1 < args.Length:
            wavDir = args[++i];
            break;
        case "--out" when i + 1 < args.Length:
            outDir = args[++i];
            break;
        case var arg when arg.StartsWith("--", StringComparison.Ordinal):
            Console.WriteLine($"Unbekannte Option {arg}");
            return 1;
        default:
            inputs.Add(args[i]);
            break;
    }
}

string[] extensions = [".m4a", ".mp4", ".aac", ".m4b"];
var files = inputs.SelectMany<string, string>(input => Directory.Exists(input)
        ? Directory.EnumerateFiles(input, "*", SearchOption.AllDirectories)
            .Where(file => extensions.Contains(Path.GetExtension(file).ToLowerInvariant()))
            .Order(StringComparer.Ordinal)
        : [input])
    .ToList();
if (files.Count == 0)
{
    Console.WriteLine("Aufruf: <datei oder ordner>... [--wav ordner] [--out ordner]");
    return 1;
}

if (NativeMethods.GetApiVersion() != NativeMethods.ApiVersion)
{
    Console.WriteLine($"jpaac hat Schnittstelle {NativeMethods.GetApiVersion()}, erwartet {NativeMethods.ApiVersion}.");
    return 1;
}

var report = new StringBuilder();
void Line(string text = "")
{
    Console.WriteLine(text);
    report.AppendLine(text);
}

Line("# Prüfpunkt 4: AAC");
Line();
Line($"- {NativeMethods.Text(NativeMethods.FfmpegVersion())}");
Line($"- {Environment.OSVersion}, .NET {Environment.Version}, {Environment.ProcessorCount} Kerne");
Line($"- Bibliotheken neben dem Programm: {LibrarySizes()}");
Line();
Line("| Datei | Codec | Rate | Kanäle | Länge laut Datei | decodiert | Öffnen | erste 5 s | Decodieren gesamt | × Echtzeit | Spitze | RMS | über 1,0 | NaN |");
Line("|---|---|---|---|---|---|---|---|---|---|---|---|---|---|");

var failed = 0;
var envelopes = new List<(string File, Envelope Envelope)>();
foreach (var file in files)
{
    try
    {
        var (result, envelope) = Decode(file, wavDir);
        Line(result.ToRow(culture));
        envelopes.Add((file, envelope));
    }
    catch (InvalidDataException exception)
    {
        failed++;
        Line($"| {Path.GetFileName(file)} | Fehler: {exception.Message} |");
    }
}

var comparisons = envelopes
    .Select(entry => (entry.File, entry.Envelope, Mp3: FindMp3(entry.File)))
    .Where(entry => entry.Mp3 is not null)
    .ToList();
if (comparisons.Count > 0)
{
    // Nur zum Decodieren, öffnet kein Gerät.
    using var engine = new MiniAudioEngine();
    Line();
    Line("Vergleich mit der MP3 derselben Aufnahme (SoundFlow):");
    Line();
    Line("| Datei | MP3 | Rate MP3 | Länge AAC | Länge MP3 | Korrelation der Hüllkurve | Versatz | Pegel MP3 − AAC | Spitze MP3 |");
    Line("|---|---|---|---|---|---|---|---|---|");
    foreach (var (file, envelope, mp3) in comparisons)
    {
        var (mp3Envelope, mp3Rate) = DecodeMp3(engine, mp3!);
        var (correlation, offset, level) = envelope.Compare(mp3Envelope);
        Line(string.Create(culture,
            $"| {Path.GetFileName(file)} | {Path.GetFileName(mp3)} | {mp3Rate} | {envelope.Seconds:0.00} s | {mp3Envelope.Seconds:0.00} s | {correlation:0.0000} | {offset:0} ms | {level:+0.00;-0.00} dB | {20 * Math.Log10(mp3Envelope.Peak):0.0} dB |"));
    }
}

Directory.CreateDirectory(outDir);
File.WriteAllText(Path.Combine(outDir, "report.md"), report.ToString());
Console.WriteLine();
Console.WriteLine($"Bericht: {Path.GetFullPath(Path.Combine(outDir, "report.md"))}");
return failed == 0 ? 0 : 2;

static string? FindMp3(string path)
{
    var stem = Path.Combine(Path.GetDirectoryName(path)!, Path.GetFileNameWithoutExtension(path));
    return new[] { stem + ".mp3", stem + "-converted.mp3" }.FirstOrDefault(File.Exists);
}

static (Envelope Envelope, int Rate) DecodeMp3(MiniAudioEngine engine, string path)
{
    using var stream = File.OpenRead(path);
    using var provider = new StreamDataProvider(engine, stream, new ReadOptions { ReadTags = false, ReadAlbumArt = false });
    var channels = provider.FormatInfo?.ChannelCount ?? 2;
    var envelope = new Envelope(channels, provider.SampleRate);
    var buffer = new float[16384 * channels];
    int read;
    while ((read = provider.ReadBytes(buffer)) > 0)
    {
        envelope.Add(buffer.AsSpan(0, read));
    }

    return (envelope, provider.SampleRate);
}

static (DecodeResult Result, Envelope Envelope) Decode(string path, string? wavDir)
{
    const int ChunkFrames = 16384;
    var total = Stopwatch.StartNew();
    var error = NativeMethods.Open(path, out var handle);
    using var aac = handle;
    if (error != 0)
    {
        throw new InvalidDataException($"jp_aac_open liefert {error}");
    }

    var openTime = total.Elapsed;
    var channels = NativeMethods.Channels(aac);
    var rate = NativeMethods.SampleRate(aac);
    var buffer = new float[ChunkFrames * channels];
    var envelope = new Envelope(channels, rate);
    using var wav = wavDir is null ? null : new WavWriter(Path.Combine(wavDir, Path.GetFileNameWithoutExtension(path) + ".wav"), channels, rate);

    long frames = 0;
    var decodeTicks = 0L; // nur die Zeit in jp_aac_read, ohne die Statistik in C#
    TimeSpan? firstFiveSeconds = null;
    float peak = 0;
    double sumSquares = 0;
    long overOne = 0;
    long nan = 0;
    while (true)
    {
        var before = Stopwatch.GetTimestamp();
        var read = NativeMethods.Read(aac, buffer, ChunkFrames);
        decodeTicks += Stopwatch.GetTimestamp() - before;
        if (read < 0)
        {
            throw new InvalidDataException($"jp_aac_read liefert {read} nach {frames} Frames");
        }

        if (read == 0)
        {
            break;
        }

        var samples = buffer.AsSpan(0, read * channels);
        foreach (var sample in samples)
        {
            if (!float.IsFinite(sample))
            {
                nan++;
                continue;
            }

            var magnitude = Math.Abs(sample);
            peak = Math.Max(peak, magnitude);
            sumSquares += sample * (double)sample;
            if (magnitude > 1.0f)
            {
                overOne++;
            }
        }

        envelope.Add(samples);
        wav?.Write(samples);
        frames += read;
        if (firstFiveSeconds is null && frames >= rate * 5L)
        {
            firstFiveSeconds = openTime + Stopwatch.GetElapsedTime(0, decodeTicks);
        }
    }

    total.Stop();
    var rms = frames > 0 ? Math.Sqrt(sumSquares / (frames * channels)) : 0;
    var codec = NativeMethods.Text(NativeMethods.Codec(aac));
    var profile = NativeMethods.Text(NativeMethods.Profile(aac));
    var result = new DecodeResult(Path.GetFileName(path), profile.Length > 0 ? $"{codec} {profile}" : codec, rate, channels,
        NativeMethods.Length(aac), frames, openTime, firstFiveSeconds, openTime + Stopwatch.GetElapsedTime(0, decodeTicks), peak, rms, overOne, nan);
    return (result, envelope);
}

static string LibrarySizes()
{
    var directory = AppContext.BaseDirectory;
    var libraries = Directory.EnumerateFiles(directory)
        .Where(file => Path.GetFileName(file) is var name
            && (name.Contains("jpaac", StringComparison.Ordinal) || name.Contains("avformat", StringComparison.Ordinal)
                || name.Contains("avcodec", StringComparison.Ordinal) || name.Contains("avutil", StringComparison.Ordinal)))
        .Select(file => new FileInfo(file))
        .OrderBy(file => file.Name, StringComparer.Ordinal)
        .ToList();
    var parts = libraries.Select(file => $"{file.Name} {file.Length / 1024.0:0} KiB");
    return $"{string.Join(", ", parts)}; zusammen {libraries.Sum(file => file.Length) / (1024.0 * 1024.0):0.00} MiB";
}

internal sealed record DecodeResult(
    string Name,
    string Codec,
    int Rate,
    int Channels,
    long ExpectedFrames,
    long Frames,
    TimeSpan Open,
    TimeSpan? FirstFiveSeconds,
    TimeSpan Total,
    float Peak,
    double Rms,
    long OverOne,
    long Nan)
{
    public string ToRow(CultureInfo culture)
    {
        var seconds = (double)Frames / Rate;
        var expected = ExpectedFrames >= 0 ? string.Create(culture, $"{ExpectedFrames} ({(double)ExpectedFrames / Rate:0.00} s)") : "?";
        var first = FirstFiveSeconds is { } time ? $"{time.TotalMilliseconds:0} ms" : "–";
        return string.Create(culture,
            $"| {Name} | {Codec} | {Rate} | {Channels} | {expected} | {Frames} ({seconds:0.00} s) | {Open.TotalMilliseconds:0} ms | {first} | {Total.TotalMilliseconds:0} ms | {seconds / Total.TotalSeconds:0} | {Decibel(Peak):0.0} dB | {Decibel(Rms):0.0} dB | {OverOne} | {Nan} |");
    }

    private static double Decibel(double value) => value > 0 ? 20 * Math.Log10(value) : double.NegativeInfinity;
}
