using System.ComponentModel;
using System.Globalization;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using JustPractise.App.FileBrowser;
using JustPractise.App.Localization;
using JustPractise.App.Shell;
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
            _services = CompositionRoot.Build();
            var translator = _services.GetRequiredService<Translator>();
            UseCulture(translator.Culture);
            translator.PropertyChanged += OnLanguageChanged;
            TrExtension.Current = translator; // vor dem ersten Fenster, damit XAML die Texte findet

            desktop.MainWindow = _services.GetRequiredService<MainWindow>();
            _ = _services.GetRequiredService<FileBrowserViewModel>().StartAsync();
            desktop.Exit += (_, _) =>
            {
                // Noch nicht geschriebene Änderungen am Gedächtnis sichern, z. B. den zuletzt geöffneten Ordner.
                _services.GetRequiredService<DataFileKeeper<DeviceMemory>>().FlushAsync().Wait(TimeSpan.FromSeconds(2));
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
