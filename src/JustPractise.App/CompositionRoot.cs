using JustPractise.App.FileBrowser;
using JustPractise.App.Localization;
using JustPractise.App.Metronome;
using JustPractise.App.Player;
using JustPractise.App.Shell;
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

        services.AddSingleton<SoundFlowOutput>();
        services.AddSingleton(sp =>
        {
            var output = sp.GetRequiredService<SoundFlowOutput>();
            return new AudioSession(output, new FileDecoder(output.Engine), sp.GetRequiredService<TimeProvider>());
        });
        services.AddSingleton<IPlayer, AudioPlayer>();
        services.AddSingleton<PlayerViewModel>();
        services.AddSingleton<IMetronome, AudioMetronome>();
        services.AddSingleton<MetronomeViewModel>();

        services.AddSingleton<MainWindowViewModel>();
        services.AddSingleton(sp => new MainWindow { DataContext = sp.GetRequiredService<MainWindowViewModel>() });

        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true,
        });
    }
}
