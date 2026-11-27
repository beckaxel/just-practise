using JustPractise.App.FileBrowser;
using JustPractise.App.Localization;
using JustPractise.App.Metronome;
using JustPractise.App.Player;
using JustPractise.App.Shell;
using JustPractise.App.Shell.ScreenSaver;
using JustPractise.App.Timer;
using JustPractise.App.Tuner;
using JustPractise.Audio.Tuner;
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
        services.AddSingleton<IRemoveFolderDialog, RemoveFolderDialog>();
        services.AddSingleton<FileBrowserViewModel>();

        // Ohne Ausgabegerät läuft die App ohne Ton weiter; die Statusleiste sagt es.
        services.AddSingleton<SoundFlowOutput>();
        services.AddSingleton(sp =>
        {
            var device = sp.GetRequiredService<SoundFlowOutput>();
            var output = new FallbackOutput(device, new SilentOutput());
            var decoder = new FormatDecoder(new FileDecoder(device.Engine), new AacDecoder());
            return new AudioSession(output, decoder, sp.GetRequiredService<TimeProvider>());
        });

        // Das Eingabegerät des Stimmgeräts nutzt dieselbe Engine; offen ist es nur, solange das Stimmgerät an ist.
        services.AddSingleton<IAudioInput>(sp => new SoundFlowInput(sp.GetRequiredService<SoundFlowOutput>().Engine));
        services.AddSingleton<PitchTracker>();
        services.AddSingleton<TunerViewModel>();
        services.AddSingleton<IAudioDevice>(sp => sp.GetRequiredService<AudioSession>());
        services.AddSingleton<IPlayer, AudioPlayer>();
        services.AddSingleton<PlayerViewModel>();
        services.AddSingleton<IMetronome, AudioMetronome>();
        services.AddSingleton<MetronomeViewModel>();
        services.AddSingleton<TimerViewModel>();

        services.AddSingleton<IUpdateService, VelopackUpdateService>();
        services.AddSingleton<UpdatesViewModel>();
        services.AddSingleton<DeveloperMode>();
        services.AddSingleton<StatusBarViewModel>();
        services.AddSingleton<SettingsViewModel>();
        services.AddSingleton<LibraryPane>();
        services.AddSingleton<AboutViewModel>();
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
