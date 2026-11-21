using Avalonia;
using JustPractise.App.Shell;
using Velopack;

namespace JustPractise.App;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        StartupProbe.Mark("main");

        // Muss vor allem anderen laufen: Velopack ruft die App beim Installieren, Aktualisieren und Entfernen
        // mit eigenen Argumenten auf und beendet sie dann hier. Ein heruntergeladenes Update wird hier installiert.
        VelopackApp.Build().Run();
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    // Wird auch vom Avalonia-Designer verwendet.
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
