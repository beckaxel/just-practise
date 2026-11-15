using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;

namespace JustPractise.Prototypes.GcCheck;

/// <summary>
/// Liest unter Linux fortlaufend die Ausgabe von <c>pw-top -b</c> und merkt sich pro PipeWire-Knoten
/// die Zahl der Fehler (Spalte ERR, vor allem xruns). So zeigen sich Aussetzer, die nicht in unserem
/// Callback entstehen, z. B. bei Bluetooth. Ohne PipeWire bleibt <see cref="IsAvailable"/> false.
/// </summary>
internal sealed class PipeWireMonitor : IDisposable
{
    private readonly Lock _lock = new();
    private readonly Dictionary<string, long> _errors = new(StringComparer.Ordinal);
    private readonly Process? _process;

    public PipeWireMonitor()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        var info = new ProcessStartInfo("pw-top", ["-b"])
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        try
        {
            _process = Process.Start(info);
        }
        catch (Win32Exception)
        {
            return;
        }

        if (_process is not null)
        {
            _process.OutputDataReceived += (_, e) => Parse(e.Data);
            _process.BeginOutputReadLine();
            _process.BeginErrorReadLine();
        }
    }

    public bool IsAvailable => _process is not null;

    /// <summary>Aktueller Stand der Fehler pro Knoten.</summary>
    public Dictionary<string, long> Snapshot()
    {
        lock (_lock)
        {
            return new Dictionary<string, long>(_errors, StringComparer.Ordinal);
        }
    }

    /// <summary>Fehler seit <paramref name="before"/>, summiert und als Liste der betroffenen Knoten.</summary>
    public (long Total, string Nodes) Since(Dictionary<string, long> before)
    {
        var now = Snapshot();
        var total = 0L;
        var nodes = new List<string>();
        foreach (var (name, errors) in now)
        {
            var delta = errors - before.GetValueOrDefault(name);
            if (delta > 0)
            {
                total += delta;
                nodes.Add($"{name} +{delta}");
            }
        }

        return (total, string.Join(", ", nodes));
    }

    public void Dispose()
    {
        if (_process is { HasExited: false })
        {
            _process.Kill();
        }

        _process?.Dispose();
    }

    /// <summary>Zeile wie „R   56   1024  48000  12,3us ... 0  S32LE 2 48000 alsa_output...“.</summary>
    private void Parse(string? line)
    {
        if (string.IsNullOrWhiteSpace(line) || line.StartsWith("S   ID", StringComparison.Ordinal))
        {
            return;
        }

        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 10 || !long.TryParse(parts[8], NumberStyles.Integer, CultureInfo.InvariantCulture, out var errors))
        {
            return;
        }

        // Der Name steht am Ende und enthält keine Leerzeichen; das Format davor ist unterschiedlich lang.
        var name = parts[^1];
        lock (_lock)
        {
            _errors[name] = Math.Max(errors, _errors.GetValueOrDefault(name));
        }
    }
}
