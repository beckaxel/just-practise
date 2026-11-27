using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Themes.Fluent;
using JustPractise.Prototypes.TouchCheck;

// Prüfpunkt 6: Touch und Stift. Ein Fenster mit Zeichenfläche und Bedienelementen; „Bericht speichern“ schreibt
// die Zähler nach touchcheck-out/. Gibt keinen Ton aus.
//
//   dotnet run --project prototypes/TouchCheck

AppBuilder.Configure<TouchApp>()
    .UsePlatformDetect()
    .WithInterFont()
    .LogToTrace()
    .StartWithClassicDesktopLifetime(args);

namespace JustPractise.Prototypes.TouchCheck
{
    internal sealed class TouchApp : Application
    {
        public override void Initialize() => Styles.Add(new FluentTheme());

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.MainWindow = new MainWindow();
            }

            base.OnFrameworkInitializationCompleted();
        }
    }
}
