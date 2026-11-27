// Plays an audio file through the audio layer of just practise, with tempo and metronome,
// to check by ear what the tests cannot hear. Opens the default output device.
//
//   dotnet run tools/play.cs -- <file> [--tempo 0.8] [--bpm 100] [--meter 3/4]
//   dotnet run tools/play.cs -- <file> --check    decode only, no audio device
//
// Keys: Space play/pause · Home or 0 back to start · Left/Right -/+ 5 s · Up/Down tempo +/- 5 %
//       m metronome on/off · +/- metronome +/- 5 BPM · 3/4 meter · v/V player volume -/+ 1 dB · q quit

#:project ../src/JustPractise.Audio/JustPractise.Audio.csproj

using System.Globalization;
using JustPractise.Audio;
using JustPractise.Audio.Engine;
using JustPractise.Audio.Metronome;
using JustPractise.Audio.Player;
using JustPractise.Core.Metronome;

var options = Options.Parse(args);
if (options is null)
{
    Console.WriteLine("Usage: dotnet run tools/play.cs -- <file> [--tempo 0.8] [--bpm 100] [--meter 3/4] [--check]");
    return 2;
}

if (!StretchVoice.IsAvailable(out var reason))
{
    Console.WriteLine(reason);
    return 1;
}

using var output = new SoundFlowOutput();
if (options.CheckOnly)
{
    DecodedAudio? audio = null;
    new FormatDecoder(new FileDecoder(output.Engine), new AacDecoder()).Decode(options.File, new BlockPool(), decoded => audio = decoded, CancellationToken.None);
    Console.WriteLine($"{Path.GetFileName(options.File)}: {audio!.Channels} channels, {audio.SampleRate} Hz, {audio.Duration:m\\:ss\\.ff}");
    Console.WriteLine("Native library jpstretch: ok");
    return 0;
}

using var session = new AudioSession(output, new FormatDecoder(new FileDecoder(output.Engine), new AacDecoder()), TimeProvider.System);
var player = new AudioPlayer(session);
var metronome = new AudioMetronome(session);

var tempo = options.Tempo;
var volumeDb = 0.0;
player.SetTempo(tempo);
metronome.SetTempo(options.Bpm);
metronome.SetMeter(options.Meter);
await player.LoadAsync(options.File);
Console.WriteLine($"{Path.GetFileName(options.File)} on {session.DeviceName ?? "default device"} at {session.SampleRate} Hz");
Console.WriteLine("Space play/pause · Home start · ←/→ 5 s · ↑/↓ tempo · m metronome · +/- BPM · 3/4 meter · v/V volume · q quit");

var running = true;
while (running)
{
    while (Console.KeyAvailable)
    {
        var key = Console.ReadKey(intercept: true);
        var state = player.State;
        switch (key.Key)
        {
            case ConsoleKey.Spacebar when state.IsPlaying:
                player.Pause();
                break;
            case ConsoleKey.Spacebar:
                player.Play();
                break;
            case ConsoleKey.Home or ConsoleKey.D0:
                player.ToStart();
                break;
            case ConsoleKey.LeftArrow:
                player.Seek(state.Position - TimeSpan.FromSeconds(5));
                break;
            case ConsoleKey.RightArrow:
                player.Seek(state.Position + TimeSpan.FromSeconds(5));
                break;
            case ConsoleKey.UpArrow:
                tempo = Math.Min(1.5, Math.Round(tempo + 0.05, 2));
                player.SetTempo(tempo);
                break;
            case ConsoleKey.DownArrow:
                tempo = Math.Max(0.5, Math.Round(tempo - 0.05, 2));
                player.SetTempo(tempo);
                break;
            case ConsoleKey.M:
                metronome.SetRunning(!metronome.IsRunning);
                break;
            case ConsoleKey.Add or ConsoleKey.OemPlus:
                metronome.SetTempo(metronome.Bpm + 5);
                break;
            case ConsoleKey.Subtract or ConsoleKey.OemMinus:
                metronome.SetTempo(metronome.Bpm - 5);
                break;
            case ConsoleKey.D3:
                metronome.SetMeter(Meter.ThreeFour);
                break;
            case ConsoleKey.D4:
                metronome.SetMeter(Meter.FourFour);
                break;
            case ConsoleKey.V:
                volumeDb += (key.Modifiers & ConsoleModifiers.Shift) != 0 ? 1 : -1;
                player.SetVolume(volumeDb);
                break;
            case ConsoleKey.Q or ConsoleKey.Escape:
                running = false;
                break;
        }
    }

    var current = player.State;
    var beat = metronome.LastBeat is { } last ? (last.IndexInBar + 1).ToString(CultureInfo.InvariantCulture) : "-";
    var monitor = session.Monitor;
    Console.Write(
        $"\r{(current.IsPlaying ? "▶" : "⏸")} {current.Position:m\\:ss\\.f}/{current.Duration:m\\:ss}" +
        $" tempo {tempo * 100:0}% vol {volumeDb:+0;-0;0}dB" +
        $" metro {(metronome.IsRunning ? "on " : "off")} {metronome.Bpm} {metronome.Meter} {beat}" +
        $" | late {monitor.LateCallbacks} gap {monitor.MaxGap.TotalMilliseconds:0}ms lead {monitor.MinLead.TotalMilliseconds:0}ms" +
        $" blk {monitor.MinFrames}-{monitor.MaxFrames}  ");
    await Task.Delay(50);
}

Console.WriteLine();
return 0;

internal sealed record Options(string File, double Tempo, int Bpm, Meter Meter, bool CheckOnly)
{
    public static Options? Parse(string[] args)
    {
        string? file = null;
        double tempo = 1;
        var bpm = 100;
        var meter = Meter.FourFour;
        var check = false;
        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--tempo" when i + 1 < args.Length && double.TryParse(args[i + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out var value):
                    tempo = Math.Clamp(value, 0.5, 1.5);
                    i++;
                    break;
                case "--bpm" when i + 1 < args.Length && int.TryParse(args[i + 1], CultureInfo.InvariantCulture, out var value):
                    bpm = value;
                    i++;
                    break;
                case "--meter" when i + 1 < args.Length && Meter.TryParse(args[i + 1], out var value):
                    meter = value;
                    i++;
                    break;
                case "--check":
                    check = true;
                    break;
                case var other when !other.StartsWith("--", StringComparison.Ordinal) && file is null:
                    file = other;
                    break;
                default:
                    return null;
            }
        }

        return file is null || !System.IO.File.Exists(file) ? null : new Options(Path.GetFullPath(file), tempo, bpm, meter, check);
    }
}
