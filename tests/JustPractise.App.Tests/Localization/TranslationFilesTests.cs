using System.Text;
using Jeffijoe.MessageFormat;
using Jeffijoe.MessageFormat.Parsing;
using JustPractise.App.Localization;

namespace JustPractise.App.Tests.Localization;

/// <summary>Prüft alle eingebetteten Sprachdateien gegen die Referenz <c>en.json</c>.</summary>
public sealed class TranslationFilesTests
{
    private static readonly Dictionary<string, Dictionary<string, string>> Files = TranslationCatalog.Embedded()
        .ToDictionary(file => file.Language, file =>
        {
            using (file.Content)
            {
                return TranslationCatalog.Parse(file.Content);
            }
        });

    public static TheoryData<string> Languages => [.. Files.Keys];

    private static Dictionary<string, string> Reference => Files[TranslationCatalog.ReferenceLanguage];

    /// <summary>Die Namen der Platzhalter auf oberster Ebene, z. B. <c>bpm</c> in <c>{bpm} BPM</c>.</summary>
    private static string[] Placeholders(string text) =>
        [.. new PatternParser().Parse(new StringBuilder(text)).Select(request => request.Variable).Order(StringComparer.Ordinal)];

    /// <summary>
    /// Jeder Text, den die Oberfläche über <c>{l:Tr …}</c> oder <c>translator.Get/Format("…")</c> anfordert, steht in
    /// der Referenz; sonst zeigt die App den Schlüssel statt des Textes.
    /// </summary>
    [Fact]
    public void ContainsEveryKeyTheAppUses()
    {
        var source = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "JustPractise.App");
        var used = Directory.EnumerateFiles(source, "*.*", SearchOption.AllDirectories)
            .Where(path => path.EndsWith(".axaml", StringComparison.Ordinal) || path.EndsWith(".cs", StringComparison.Ordinal))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .SelectMany(path => System.Text.RegularExpressions.Regex.Matches(
                File.ReadAllText(path),
                @"\{l:Tr ([a-zA-Z0-9_.]+)\}|translator\.(?:Get|Format)\(""([a-zA-Z0-9_.]+)""")
                .Select(match => match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value))
            .Distinct()
            .ToList();

        Assert.NotEmpty(used);
        var missing = used.Where(key => !Reference.ContainsKey(key)).ToList();
        Assert.True(missing.Count == 0, "Fehlende Texte: " + string.Join(", ", missing));
    }

    [Fact]
    public void ContainsGermanAndEnglish()
    {
        Assert.Contains("en", Files.Keys);
        Assert.Contains("de", Files.Keys);
    }

    [Theory]
    [MemberData(nameof(Languages))]
    public void NamesTheLanguage(string language) =>
        Assert.False(string.IsNullOrWhiteSpace(Files[language].GetValueOrDefault("language.name")));

    [Theory]
    [MemberData(nameof(Languages))]
    public void HasNoKeysMissingFromTheReference(string language) =>
        Assert.Empty(Files[language].Keys.Except(Reference.Keys));

    [Theory]
    [MemberData(nameof(Languages))]
    public void UsesTheSamePlaceholdersAsTheReference(string language)
    {
        foreach (var (key, text) in Files[language])
        {
            Assert.True(
                Placeholders(text).SequenceEqual(Placeholders(Reference[key])),
                $"{language}: {key} has other placeholders than en");
        }
    }

    [Theory]
    [MemberData(nameof(Languages))]
    public void FormatsEveryText(string language)
    {
        var formatter = new MessageFormatter();
        foreach (var (key, text) in Files[language])
        {
            var arguments = Placeholders(text).Distinct().ToDictionary(name => name, object? (_) => 2);
            var exception = Record.Exception(() => formatter.FormatMessage(text, arguments));
            Assert.True(exception is null, $"{language}: {key}: {exception?.Message}");
        }
    }
}
