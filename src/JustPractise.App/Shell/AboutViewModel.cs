using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JustPractise.App.Localization;

namespace JustPractise.App.Shell;

/// <summary>Ein Open-Source-Baustein der App: Name, wofür (Schlüssel der Übersetzung), Lizenz und Projektseite.</summary>
public sealed record Component(string Name, string PurposeKey, string License, Uri Project);

/// <summary>Ein Baustein, wie das Fenster ihn zeigt: wofür, in der Sprache der Oberfläche.</summary>
public sealed record ComponentRow(string Name, string Purpose, string License, Uri Project);

/// <summary>
/// „Über just practise“: Version, Quellcode, Einladung zum Mitmachen, die verwendeten Open-Source-Bausteine und auf
/// Wunsch die vollständigen Lizenztexte (dieselben wie in <c>THIRD-PARTY-NOTICES.txt</c> im Paket).
/// </summary>
public sealed partial class AboutViewModel(Translator translator)
{
    public static Uri SourceCode { get; } = new("https://github.com/beckaxel/just-practise");

    public static Uri Issues { get; } = new("https://github.com/beckaxel/just-practise/issues");

    public static Uri Contributing { get; } = new("https://github.com/beckaxel/just-practise/blob/main/CONTRIBUTING.md");

    /// <summary>Die Bausteine, die mit der App ausgeliefert werden; die Lizenztexte stehen in THIRD-PARTY-NOTICES.txt.</summary>
    public static IReadOnlyList<Component> Components { get; } =
    [
        new("Avalonia", "about.purpose.ui", "MIT", new("https://avaloniaui.net")),
        new("SkiaSharp / Skia", "about.purpose.drawing", "MIT / BSD-3-Clause", new("https://github.com/mono/SkiaSharp")),
        new("HarfBuzzSharp / HarfBuzz", "about.purpose.text", "MIT", new("https://harfbuzz.github.io")),
        new("ANGLE", "about.purpose.graphicsWindows", "BSD-3-Clause", new("https://chromium.googlesource.com/angle/angle")),
        new("Inter", "about.purpose.font", "SIL OFL 1.1", new("https://github.com/rsms/inter")),
        new("SoundFlow / miniaudio", "about.purpose.audio", "MIT", new("https://github.com/LSXPrime/SoundFlow")),
        new("signalsmith-stretch", "about.purpose.stretch", "MIT", new("https://github.com/Signalsmith-Audio/signalsmith-stretch")),
        new("Velopack", "about.purpose.updates", "MIT", new("https://velopack.io")),
        new("CommunityToolkit.Mvvm", "about.purpose.mvvm", "MIT", new("https://github.com/CommunityToolkit/dotnet")),
        new("MessageFormat", "about.purpose.translations", "MIT", new("https://github.com/jeffijoe/messageformat.net")),
        new("Tmds.DBus.Protocol", "about.purpose.linux", "MIT", new("https://github.com/tmds/Tmds.DBus")),
        new(".NET", "about.purpose.runtime", "MIT", new("https://dotnet.microsoft.com")),
    ];

    /// <summary>Die Bausteine in der Sprache der Oberfläche.</summary>
    public IReadOnlyList<ComponentRow> Rows =>
        [.. Components.Select(component => new ComponentRow(component.Name, translator.Get(component.PurposeKey), component.License, component.Project))];

    /// <summary>„Version 0.1.0-rc.5“; ein Bau aus dem Quellcode ohne Versionsangabe heißt „Entwicklungsversion“.</summary>
    public string VersionText
    {
        get
        {
            var version = typeof(AboutViewModel).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                .Split('+')[0];
            return version is null or "1.0.0"
                ? translator.Get("about.devVersion")
                : translator.Format("about.version", new Dictionary<string, object?> { ["version"] = version });
        }
    }

    /// <summary>Die vollständigen Lizenztexte: die eigene MIT-Lizenz, dann die der Bausteine. Wird erst beim Zeigen gelesen.</summary>
    public string LicenseText => _licenseText ??= Read("LICENSE") + "\n\n" + Read("THIRD-PARTY-NOTICES.txt");

    private string? _licenseText;

    private static string Read(string name)
    {
        using var stream = typeof(AboutViewModel).Assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"{name} fehlt in der App.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
