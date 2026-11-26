using System.Globalization;
using System.Text.RegularExpressions;
using JustPractise.App.Localization;
using JustPractise.App.Shell;

namespace JustPractise.App.Tests.Shell;

public sealed partial class AboutViewModelTests
{
    private static readonly string Root = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..");

    private readonly AboutViewModel _about = new(new Translator(TranslationCatalog.Load(null), "de", CultureInfo.GetCultureInfo("de-DE")));

    [Fact]
    public void ShipsTheOwnLicenseAndTheThirdPartyNotices()
    {
        var text = _about.LicenseText;

        Assert.Contains("MIT License", text, StringComparison.Ordinal);
        Assert.Contains("just practise – third-party notices", text, StringComparison.Ordinal);
        Assert.Contains("SIL OPEN FONT LICENSE", text, StringComparison.Ordinal);
    }

    [Fact]
    public void NamesThePurposeOfEveryComponentInBothLanguages()
    {
        var english = new AboutViewModel(new Translator(TranslationCatalog.Load(null), "en", CultureInfo.GetCultureInfo("en-US")));

        Assert.All(_about.Rows, row => Assert.DoesNotContain("about.purpose", row.Purpose, StringComparison.Ordinal));
        Assert.All(english.Rows, row => Assert.DoesNotContain("about.purpose", row.Purpose, StringComparison.Ordinal));
        Assert.Equal("Oberfläche", _about.Rows[0].Purpose);
        Assert.Equal("User interface", english.Rows[0].Purpose);
    }

    [Fact]
    public void ShowsADevelopmentVersionWhenBuiltFromSource()
    {
        Assert.Equal("Entwicklungsversion", _about.VersionText);
    }

    /// <summary>
    /// Jedes Paket, das die App einbindet, braucht seinen Lizenztext in THIRD-PARTY-NOTICES.txt; sonst fehlt er im
    /// Paket, obwohl die meisten Lizenzen verlangen, dass er mitgeliefert wird.
    /// </summary>
    [Fact]
    public void ListsEveryPackageOfTheAppInTheNotices()
    {
        var notices = File.ReadAllText(Path.Combine(Root, "THIRD-PARTY-NOTICES.txt"));
        var packages = File.ReadAllText(Path.Combine(Root, "Directory.Packages.props"));
        var shipped = PackageVersion().Matches(packages)
            .Select(match => (Name: match.Groups[1].Value, Version: match.Groups[2].Value))
            .Where(package => !package.Name.StartsWith("xunit", StringComparison.Ordinal))
            .ToList();

        Assert.NotEmpty(shipped);
        foreach (var (name, version) in shipped)
        {
            // Pakete von Avalonia sind unter „Avalonia“ zusammengefasst.
            var listed = name.StartsWith("Avalonia", StringComparison.Ordinal)
                ? notices.Contains($"Avalonia {version}", StringComparison.Ordinal)
                : notices.Contains($"{name} {version}", StringComparison.Ordinal);
            Assert.True(listed, $"{name} {version} fehlt in THIRD-PARTY-NOTICES.txt");
        }
    }

    [GeneratedRegex("""<PackageVersion Include="([^"]+)" Version="([^"]+)" />""")]
    private static partial Regex PackageVersion();
}
