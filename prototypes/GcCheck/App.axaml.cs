using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

namespace JustPractise.Prototypes.GcCheck;

internal sealed partial class App : Application
{
    // Ein Prototyp ohne DI: Program.cs legt die Sitzung vor dem Start von Avalonia an.
    public static AudioSession? Session { get; set; }

    public static Options? Options { get; set; }

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop && Session is not null && Options is not null)
        {
            desktop.MainWindow = new MainWindow(Session, Options);
        }

        base.OnFrameworkInitializationCompleted();
    }
}
