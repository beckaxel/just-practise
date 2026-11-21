using System.Globalization;

namespace JustPractise.Prototypes.GcCheck;

/// <summary>Einstellungen von der Kommandozeile, die nur beim Start wirken.</summary>
internal sealed record Options(
    string File,
    uint BufferMilliseconds,
    double StartSeconds,
    double? Seconds,
    bool WarmUp,
    int StretchOptions,
    bool Auto,
    bool Matrix,
    double PhaseSeconds,
    string Report)
{
    public const string Usage =
        "Aufruf: <datei> [--buffer ms] [--start s] [--seconds s] [--no-warmup] [--cheaper]\n" +
        "               [--auto] [--matrix] [--phase-seconds s] [--report datei]";

    public static Options? Parse(string[] args)
    {
        if (args.Length < 1 || args[0].StartsWith("--", StringComparison.Ordinal))
        {
            return null;
        }

        var culture = CultureInfo.InvariantCulture;
        var options = new Options(args[0], 10, 0, 120, WarmUp: true, StretchOptions: 0,
            Auto: false, Matrix: false, PhaseSeconds: 20, Report: Path.Combine("gccheck-out", "report.md"));
        for (var i = 1; i < args.Length; i++)
        {
            var value = i + 1 < args.Length ? args[i + 1] : null;
            switch (args[i])
            {
                case "--buffer" when value is not null:
                    options = options with { BufferMilliseconds = uint.Parse(value, culture) };
                    i++;
                    break;
                case "--start" when value is not null:
                    options = options with { StartSeconds = double.Parse(value, culture) };
                    i++;
                    break;
                case "--seconds" when value is not null:
                    options = options with { Seconds = double.Parse(value, culture) };
                    i++;
                    break;
                case "--phase-seconds" when value is not null:
                    options = options with { PhaseSeconds = double.Parse(value, culture) };
                    i++;
                    break;
                case "--report" when value is not null:
                    options = options with { Report = value };
                    i++;
                    break;
                case "--no-warmup":
                    options = options with { WarmUp = false };
                    break;
                case "--cheaper":
                    options = options with { StretchOptions = NativeMethods.OptionCheaper };
                    break;
                case "--auto":
                    options = options with { Auto = true };
                    break;
                case "--matrix":
                    options = options with { Matrix = true };
                    break;
                default:
                    return null;
            }
        }

        return options;
    }

    /// <summary>Argumente für einen Kindprozess im automatischen Lauf.</summary>
    public List<string> ToAutoArguments(uint bufferMilliseconds, bool warmUp)
    {
        var culture = CultureInfo.InvariantCulture;
        List<string> args =
        [
            File, "--auto",
            "--buffer", bufferMilliseconds.ToString(culture),
            "--start", StartSeconds.ToString(culture),
            "--phase-seconds", PhaseSeconds.ToString(culture),
            "--report", Report,
        ];
        if (Seconds is { } seconds)
        {
            args.AddRange(["--seconds", seconds.ToString(culture)]);
        }

        if (!warmUp)
        {
            args.Add("--no-warmup");
        }

        if (StretchOptions != 0)
        {
            args.Add("--cheaper");
        }

        return args;
    }
}
