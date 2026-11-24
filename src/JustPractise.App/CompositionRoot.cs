using JustPractise.App.FileBrowser;
using JustPractise.App.Localization;
using JustPractise.App.Metronome;
using JustPractise.App.Player;
using JustPractise.App.Shell;
using JustPractise.App.Shell.ScreenSaver;
using JustPractise.App.Updates;
using JustPractise.Audio;
using JustPractise.Audio.Engine;
using JustPractise.Audio.Metronome;
using JustPractise.Audio.Player;
using JustPractise.Core.Settings;
using JustPractise.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace JustPractise.App;

/// <summary>
/// Der einzige Ort, an dem der DI-Container angefasst wird.
/// Hier steht, welche Klasse wofür verwendet wird.
/// </summary>
internal static class CompositionRoot
{
    public static ServiceProvider Build()
    {
        var services = new ServiceCollection();

        services.AddSingleton(TimeProvider.System);
        services.AddSingleton(AppDataLocation.System);

        services.AddSingleton(sp => TranslationCatalog.Load(sp.GetRequiredService<AppDataLocation>().TranslationsDirectory));
        services.AddSingleton(sp => new DataFileKeeper<AppSettings>(sp.GetRequiredService<AppDataLocation>().Settings()));
        services.AddSingleton(sp => new Translator(
            sp.GetRequiredService<TranslationCatalog>(),
            sp.GetRequiredService<DataFileKeeper<AppSettings>>().Value.Language.Ui));

        services.AddSingleton(sp => new DataFileKeeper<DeviceMemory>(sp.GetRequiredService<AppDataLocation>().Device()));
        services.AddSingleton<IFolderPicker, StorageFolderPicker>();
        services.AddSingleton<FileBrowserViewModel>();

        // Ohne Ausgabegerät läuft die App ohne Ton weiter; die Statusleiste sagt es.
        services.AddSingleton(sp =>
        {
            var device = new SoundFlowOutput();
            var output = new FallbackOutput(device, new SilentOutput());
            return new AudioSession(output, new FileDecoder(device.Engine), sp.GetRequiredService<TimeProvider>());
        });
        services.AddSingleton<IAudioDevice>(sp => sp.GetRequiredService<AudioSession>());
        services.AddSingleton<IPlayer, AudioPlayer>();
        services.AddSingleton<PlayerViewModel>();
        services.AddSingleton<IMetronome, AudioMetronome>();
        services.AddSingleton<MetronomeViewModel>();

        services.AddSingleton<IUpdateService, VelopackUpdateService>();
        services.AddSingleton<UpdatesViewModel>();
        services.AddSingleton<DeveloperMode>();
        services.AddSingleton<StatusBarViewModel>();
        services.AddSingleton<SettingsViewModel>();
        services.AddSingleton<MainWindowViewModel>();
        services.AddSingleton<MainWindowPlacement>();
        services.AddSingleton(sp =>
        {
            var window = new MainWindow { DataContext = sp.GetRequiredService<MainWindowViewModel>() };
            sp.GetRequiredService<MainWindowPlacement>().Attach(window);
            return window;
        });
        services.AddSingleton<IScreenSaverInhibitor>(_ =>
            OperatingSystem.IsWindows() ? new WindowsScreenSaverInhibitor()
            : OperatingSystem.IsLinux() ? new FreedesktopScreenSaverInhibitor()
            : new NoScreenSaverInhibitor());
        services.AddSingleton<KeepScreenAwake>();

        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true,
        });
    }
}
