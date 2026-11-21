using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using Jeffijoe.MessageFormat;

namespace JustPractise.App.Localization;

/// <summary>Eine Sprache für die Auswahl: Code und Name in der Sprache selbst.</summary>
public sealed record LanguageOption(string Code, string Name);

/// <summary>
/// Liefert die Texte der Oberfläche in der gewählten Sprache. Fehlt ein Text, kommt der englische, fehlt auch der,
/// der Schlüssel selbst. Ein Wechsel der Sprache meldet sich über <see cref="Language"/>, die Texte folgen ohne Neustart.
/// </summary>
public sealed partial class Translator : ObservableObject
{
    private const string NameKey = "language.name";

    private readonly TranslationCatalog _catalog;
    private readonly CultureInfo _systemCulture;
    private IReadOnlyDictionary<string, string> _texts = new Dictionary<string, string>();
    private readonly IReadOnlyDictionary<string, string> _reference;
    private MessageFormatter _formatter = new();

    /// <param name="language">Gewählter Sprachcode oder <c>null</c> für die Sprache des Systems.</param>
    /// <param name="systemCulture">Die Kultur des Systems; ohne Angabe <see cref="CultureInfo.CurrentUICulture"/>.</param>
    public Translator(TranslationCatalog catalog, string? language, CultureInfo? systemCulture = null)
    {
        _catalog = catalog;
        _systemCulture = systemCulture ?? CultureInfo.CurrentUICulture;
        _reference = catalog.Texts(TranslationCatalog.ReferenceLanguage) ?? new Dictionary<string, string>();
        Languages = [.. catalog.Languages
            .Select(code => new LanguageOption(code, catalog.Texts(code)!.GetValueOrDefault(NameKey) ?? code))
            .OrderBy(option => option.Name, StringComparer.CurrentCultureIgnoreCase)];
        SetLanguage(language);
    }

    /// <summary>Der aktive Sprachcode, z. B. <c>de</c>.</summary>
    [ObservableProperty]
    public partial string Language { get; private set; } = TranslationCatalog.ReferenceLanguage;

    /// <summary>Die Kultur für Zahlen und Datumsangaben, passend zur Sprache.</summary>
    public CultureInfo Culture { get; private set; } = CultureInfo.InvariantCulture;

    /// <summary>Alle verfügbaren Sprachen, nach Namen sortiert.</summary>
    public IReadOnlyList<LanguageOption> Languages { get; }

    /// <summary>Wechselt die Sprache. <c>null</c> oder eine unbekannte Sprache folgt dem System, sonst Englisch.</summary>
    public void SetLanguage(string? language)
    {
        var code = Resolve(language);
        _texts = _catalog.Texts(code) ?? _reference;
        Culture = CultureFor(code);
        _formatter = new MessageFormatter(useCache: true, Culture);
        Language = code;
    }

    /// <summary>Der Text zu <paramref name="key"/> ohne Platzhalter.</summary>
    public string Get(string key) => _texts.GetValueOrDefault(key) ?? _reference.GetValueOrDefault(key) ?? key;

    /// <summary>Der Text zu <paramref name="key"/> mit Platzhaltern und Mehrzahl im ICU-Format.</summary>
    public string Format(string key, IReadOnlyDictionary<string, object?> arguments)
    {
        var pattern = Get(key);
        try
        {
            return _formatter.FormatMessage(pattern, arguments);
        }
        catch (Exception exception) when (exception is MessageFormatterException or FormatException or KeyNotFoundException)
        {
            return pattern;
        }
    }

    private string Resolve(string? language)
    {
        if (language is not null && _catalog.Texts(language) is not null)
        {
            return language;
        }

        var system = _systemCulture.TwoLetterISOLanguageName;
        return _catalog.Texts(system) is not null ? system : TranslationCatalog.ReferenceLanguage;
    }

    /// <summary>Die Kultur zur Sprache; passt die des Systems dazu (z. B. de-AT zu de), wird sie verwendet.</summary>
    private CultureInfo CultureFor(string code)
    {
        if (string.Equals(_systemCulture.TwoLetterISOLanguageName, code, StringComparison.OrdinalIgnoreCase))
        {
            return _systemCulture;
        }

        try
        {
            return CultureInfo.GetCultureInfo(code);
        }
        catch (CultureNotFoundException)
        {
            return CultureInfo.InvariantCulture;
        }
    }
}
