using System.ComponentModel;
using System.Globalization;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using JustPractise.App.FileBrowser;
using JustPractise.App.Localization;
using JustPractise.App.Shell;
using JustPractise.Audio;
using JustPractise.Core.Settings;
using JustPractise.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace JustPractise.App;

public sealed partial class App : Application
{
    private ServiceProvider? _services;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            StartupProbe.Mark("avalonia");
            _services = CompositionRoot.Build();
            var translator = _services.GetRequiredService<Translator>();
            StartupProbe.Mark("translations");
            _services.GetRequiredService<IAudioDevice>(); // öffnet das Gerät; hier nur vorgezogen, um es einzeln zu messen
            StartupProbe.Mark("audio");
            UseCulture(translator.Culture);
            translator.PropertyChanged += OnLanguageChanged;
            TrExtension.Current = translator; // vor dem ersten Fenster, damit XAML die Texte findet

            desktop.MainWindow = _services.GetRequiredService<MainWindow>();
            StartupProbe.Mark("window");
            StartupProbe.Attach(desktop.MainWindow, desktop);
            _ = _services.GetRequiredService<FileBrowserViewModel>().StartAsync();
            var shell = _services.GetRequiredService<MainWindowViewModel>();
            var ticker = DispatcherTimer.Run(() => { shell.Tick(); return true; }, TimeSpan.FromMilliseconds(33));
            desktop.Exit += (_, _) =>
            {
                ticker.Dispose();
                // Noch nicht geschriebene Änderungen sichern: Gedächtnis (z. B. letzter Ordner), Einstellungen und Sidecar-Datei.
                Task.WhenAll(
                        _services.GetRequiredService<DataFileKeeper<DeviceMemory>>().FlushAsync(),
                        _services.GetRequiredService<DataFileKeeper<AppSettings>>().FlushAsync(),
                        shell.Player.FlushAsync())
                    .Wait(TimeSpan.FromSeconds(2));
                _services.Dispose();
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static void OnLanguageChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is Translator translator && e.PropertyName == nameof(Translator.Language))
        {
            UseCulture(translator.Culture);
        }
    }

    /// <summary>Die Sprache der Oberfläche legt auch das Format von Zahlen und Datumsangaben fest.</summary>
    private static void UseCulture(CultureInfo culture)
    {
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
    }
}
