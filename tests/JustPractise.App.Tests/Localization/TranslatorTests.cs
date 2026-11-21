using System.ComponentModel;
using System.Globalization;
using JustPractise.App.Localization;

namespace JustPractise.App.Tests.Localization;

public sealed class TranslatorTests
{
    private static readonly CultureInfo English = CultureInfo.GetCultureInfo("en-US");

    private static TranslationCatalog Catalog() => new(new Dictionary<string, IReadOnlyDictionary<string, string>>
    {
        ["en"] = new Dictionary<string, string>
        {
            ["language.name"] = "English",
            ["player.play"] = "Play",
            ["player.only"] = "Only in English",
            ["timer.left"] = "{minutes, plural, one {# minute left} other {# minutes left}}",
        },
        ["de"] = new Dictionary<string, string>
        {
            ["language.name"] = "Deutsch",
            ["player.play"] = "Abspielen",
            ["timer.left"] = "{minutes, plural, one {noch # Minute} other {noch # Minuten}}",
        },
    });

    [Fact]
    public void UsesTheChosenLanguage()
    {
        var translator = new Translator(Catalog(), "de", English);

        Assert.Equal("de", translator.Language);
        Assert.Equal("Abspielen", translator.Get("player.play"));
    }

    [Fact]
    public void FallsBackToEnglishAndThenToTheKey()
    {
        var translator = new Translator(Catalog(), "de", English);

        Assert.Equal("Only in English", translator.Get("player.only"));
        Assert.Equal("player.missing", translator.Get("player.missing"));
    }

    [Fact]
    public void FollowsTheSystemWithItsRegionalFormat()
    {
        var austrian = CultureInfo.GetCultureInfo("de-AT");

        var translator = new Translator(Catalog(), language: null, austrian);

        Assert.Equal("de", translator.Language);
        Assert.Same(austrian, translator.Culture);
    }

    [Fact]
    public void UsesEnglishForAnUnknownSystemLanguage()
    {
        var translator = new Translator(Catalog(), language: "xx", CultureInfo.GetCultureInfo("ja-JP"));

        Assert.Equal("en", translator.Language);
    }

    [Fact]
    public void ReportsALanguageChange()
    {
        var translator = new Translator(Catalog(), "en", English);
        var changed = new List<string?>();
        translator.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        translator.SetLanguage("de");

        Assert.Contains(nameof(Translator.Language), changed);
        Assert.Equal("Abspielen", translator.Get("player.play"));
        Assert.Equal("de", translator.Culture.TwoLetterISOLanguageName);
    }

    [Fact]
    public void FormatsPlurals()
    {
        var translator = new Translator(Catalog(), "de", English);

        Assert.Equal("noch 1 Minute", translator.Format("timer.left", new Dictionary<string, object?> { ["minutes"] = 1 }));
        Assert.Equal("noch 5 Minuten", translator.Format("timer.left", new Dictionary<string, object?> { ["minutes"] = 5 }));
    }

    [Fact]
    public void ShowsThePatternWhenAnArgumentIsMissing()
    {
        var translator = new Translator(Catalog(), "en", English);

        Assert.Equal(translator.Get("timer.left"), translator.Format("timer.left", new Dictionary<string, object?>()));
    }

    [Fact]
    public void ListsTheLanguagesByName()
    {
        var translator = new Translator(Catalog(), "en", English);

        Assert.Equal(["Deutsch", "English"], translator.Languages.Select(option => option.Name));
    }

    [Fact]
    public void ImplementsPropertyChangeNotification() =>
        Assert.IsAssignableFrom<INotifyPropertyChanged>(new Translator(Catalog(), "en", English));
}
