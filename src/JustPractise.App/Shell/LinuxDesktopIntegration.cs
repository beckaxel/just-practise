using Avalonia.Platform;
using JustPractise.Storage;

namespace JustPractise.App.Shell;

/// <summary>Meldet das AppImage beim Desktop an (siehe <see cref="DesktopEntry"/>).</summary>
internal static class LinuxDesktopIntegration
{
    /// <summary>
    /// Nur für das AppImage: Dessen Laufzeit setzt <c>$APPIMAGE</c> auf den echten Pfad der Datei.
    /// Beim Start aus dem Quellcode passiert nichts. Läuft im Hintergrund, damit der Start nicht wartet.
    /// </summary>
    public static void Start()
    {
        if (!OperatingSystem.IsLinux() || Environment.GetEnvironmentVariable("APPIMAGE") is not { Length: > 0 } appImage)
        {
            return;
        }

        byte[] icon;
        using (var stream = AssetLoader.Open(new Uri("avares://just-practise/Assets/icon.png")))
        using (var buffer = new MemoryStream())
        {
            stream.CopyTo(buffer);
            icon = buffer.ToArray();
        }

        _ = Task.Run(() =>
        {
            try
            {
                DesktopEntry.System.Update(appImage, icon);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // Ohne Eintrag läuft die App genauso, nur das Anpinnen klappt nicht. Beim nächsten Start erneut.
            }
        });
    }
}
