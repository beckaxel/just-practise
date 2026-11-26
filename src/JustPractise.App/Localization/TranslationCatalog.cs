using System.Reflection;
using System.Text.Json;

namespace JustPractise.App.Localization;

/// <summary>
/// Alle Sprachdateien: die eingebetteten aus <c>Localization/*.json</c> und eigene aus dem Ordner <c>translations</c>
/// im Anwendungsdatenordner. Eine eigene Datei ersetzt die eingebettete derselben Sprache.
/// </summary>
/// <remarks>Die verschachtelten Objekte werden zu Schlüsseln mit Punkten, z. B. <c>player.playPause</c>.</remarks>
public sealed class TranslationCatalog(IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> languages)
{
    /// <summary>Die Referenzsprache: Neue Texte kommen zuerst hierher, fehlende Texte anderer Sprachen kommen von hier.</summary>
    public const string ReferenceLanguage = "en";

    private const string ResourcePrefix = "Localization.";
    private const string Extension = ".json";

    /// <summary>Die Sprachcodes, z. B. <c>de</c> und <c>en</c>.</summary>
    public IEnumerable<string> Languages => languages.Keys;

    /// <summary>Die Texte einer Sprache oder <c>null</c>, wenn es sie nicht gibt.</summary>
    public IReadOnlyDictionary<string, string>? Texts(string language) => languages.GetValueOrDefault(language);

    /// <summary>Lädt die eingebetteten Dateien und danach die aus <paramref name="userDirectory"/>, falls es ihn gibt.</summary>
    /// <remarks>Eine unlesbare eigene Datei wird übersprungen; die eingebettete Sprache bleibt dann erhalten.</remarks>
    public static TranslationCatalog Load(string? userDirectory)
    {
        var languages = new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var (language, stream) in Embedded())
        {
            using (stream)
            {
                languages[language] = Parse(stream);
            }
        }

        if (userDirectory is not null && Directory.Exists(userDirectory))
        {
            foreach (var path in Directory.EnumerateFiles(userDirectory, "*" + Extension))
            {
                try
                {
                    using var stream = File.OpenRead(path);
                    languages[Path.GetFileNameWithoutExtension(path)] = Parse(stream);
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
                {
                    // Eine kaputte eigene Datei soll die App nicht aufhalten.
                }
            }
        }

        return new TranslationCatalog(languages);
    }

    /// <summary>Die eingebetteten Sprachdateien als Sprachcode und Inhalt.</summary>
    public static IEnumerable<(string Language, Stream Content)> Embedded()
    {
        var assembly = typeof(TranslationCatalog).Assembly;
        foreach (var name in assembly.GetManifestResourceNames())
        {
            if (name.StartsWith(ResourcePrefix, StringComparison.Ordinal) && name.EndsWith(Extension, StringComparison.Ordinal))
            {
                var language = name[ResourcePrefix.Length..^Extension.Length];
                yield return (language, assembly.GetManifestResourceStream(name)!);
            }
        }
    }

    /// <summary>Liest eine Sprachdatei in flache Schlüssel. Wirft <see cref="JsonException"/> bei ungültigem Inhalt.</summary>
    public static Dictionary<string, string> Parse(Stream json)
    {
        using var document = JsonDocument.Parse(json, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
        var texts = new Dictionary<string, string>(StringComparer.Ordinal);
        Flatten(document.RootElement, prefix: null, texts);
        return texts;
    }

    private static void Flatten(JsonElement element, string? prefix, Dictionary<string, string> texts)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    Flatten(property.Value, prefix is null ? property.Name : $"{prefix}.{property.Name}", texts);
                }

                break;
            case JsonValueKind.String when prefix is not null:
                texts[prefix] = element.GetString()!;
                break;
            default:
                throw new JsonException($"'{prefix}' must be a text or an object.");
        }
    }
}
