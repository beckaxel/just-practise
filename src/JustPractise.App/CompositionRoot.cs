using JustPractise.App.Shell;
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

        services.AddSingleton<MainWindowViewModel>();
        services.AddSingleton(sp => new MainWindow { DataContext = sp.GetRequiredService<MainWindowViewModel>() });

        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true,
        });
    }
}
