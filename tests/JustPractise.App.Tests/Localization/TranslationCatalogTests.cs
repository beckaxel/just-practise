using System.Text;
using System.Text.Json;
using JustPractise.App.Localization;

namespace JustPractise.App.Tests.Localization;

public sealed class TranslationCatalogTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "jp-tests-" + Guid.NewGuid().ToString("N"));

    public TranslationCatalogTests() => Directory.CreateDirectory(_directory);

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    private static Dictionary<string, string> Parse(string json) => TranslationCatalog.Parse(new MemoryStream(Encoding.UTF8.GetBytes(json)));

    [Fact]
    public void FlattensNestedObjectsIntoDottedKeys()
    {
        var texts = Parse("""{ "player": { "play": "Play", "tempo": { "label": "Tempo" } } }""");

        Assert.Equal("Play", texts["player.play"]);
        Assert.Equal("Tempo", texts["player.tempo.label"]);
    }

    [Fact]
    public void RejectsValuesThatAreNotText() =>
        Assert.ThrowsAny<JsonException>(() => Parse("""{ "player": { "play": 1 } }"""));

    [Fact]
    public void LoadsTheEmbeddedLanguagesWithoutAUserFolder()
    {
        var catalog = TranslationCatalog.Load(Path.Combine(_directory, "missing"));

        Assert.Equal("Deutsch", catalog.Texts("de")!["language.name"]);
    }

    [Fact]
    public void LetsAUserFileReplaceOrAddALanguage()
    {
        File.WriteAllText(Path.Combine(_directory, "de.json"), """{ "language": { "name": "Deutsch (eigene)" } }""");
        File.WriteAllText(Path.Combine(_directory, "fr.json"), """{ "language": { "name": "Français" } }""");

        var catalog = TranslationCatalog.Load(_directory);

        Assert.Equal("Deutsch (eigene)", catalog.Texts("de")!["language.name"]);
        Assert.Equal("Français", catalog.Texts("fr")!["language.name"]);
    }

    [Fact]
    public void SkipsABrokenUserFile()
    {
        File.WriteAllText(Path.Combine(_directory, "de.json"), "{ kaputt");

        var catalog = TranslationCatalog.Load(_directory);

        Assert.Equal("Deutsch", catalog.Texts("de")!["language.name"]);
    }
}
