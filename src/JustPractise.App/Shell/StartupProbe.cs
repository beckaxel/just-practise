using System.Diagnostics;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;

namespace JustPractise.App.Shell;

/// <summary>
/// Misst die Startzeit (Prüfpunkt 9, <c>tools/startup.cs</c>). Nennt die Umgebungsvariable eine Datei,
/// hängt die App dort die Zeiten vom Prozessstart bis zu jedem Messpunkt an, zuletzt bis zum geöffneten
/// Fenster und zum ersten gezeichneten Bild, und beendet sich danach. Ohne die Variable passiert nichts.
/// </summary>
internal static class StartupProbe
{
    public const string Variable = "JUST_PRACTISE_STARTUP_PROBE";

    private static readonly string? s_file = Environment.GetEnvironmentVariable(Variable);
    private static readonly List<(string Name, TimeSpan Time)> s_marks = [];

    /// <summary>Hält fest, wie lange der Prozess bis hierher gebraucht hat.</summary>
    public static void Mark(string name)
    {
        if (s_file is not null)
        {
            s_marks.Add((name, SinceProcessStart()));
        }
    }

    public static void Attach(Window window, IClassicDesktopStyleApplicationLifetime desktop)
    {
        if (s_file is null)
        {
            return;
        }

        window.Opened += (_, _) =>
        {
            Mark("opened");

            // Der erste Rückruf kommt vor dem ersten Bild, der zweite erst, wenn es gezeichnet ist.
            window.RequestAnimationFrame(_ => window.RequestAnimationFrame(_ =>
            {
                Mark("frame");
                var line = string.Join(' ', s_marks.Select(mark =>
                    string.Create(CultureInfo.InvariantCulture, $"{mark.Name}={mark.Time.TotalMilliseconds:F0}")));
                File.AppendAllText(s_file, line + Environment.NewLine);
                desktop.Shutdown();
            }));
        };
    }

    private static TimeSpan SinceProcessStart()
    {
        using var process = Process.GetCurrentProcess();
        return DateTime.Now - process.StartTime;
    }
}
