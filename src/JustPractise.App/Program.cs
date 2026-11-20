using Avalonia;
using JustPractise.App.Shell;

namespace JustPractise.App;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        StartupProbe.Mark("main");
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    // Wird auch vom Avalonia-Designer verwendet.
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
