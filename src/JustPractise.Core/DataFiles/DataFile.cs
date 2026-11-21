using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace JustPractise.Core.DataFiles;

/// <summary>
/// Liest und schreibt eine Datendatei nach den Regeln aus <c>docs/technologie.md</c> (Datenhaltung):
/// <c>format</c> zuerst, nur Abweichungen, unbekannte Felder bleiben erhalten,
/// neuere und beschädigte Dateien werden nicht überschrieben.
/// </summary>
public sealed class DataFile<T>
    where T : class, new()
{
    private readonly JsonTypeInfo<T> _typeInfo;
    private readonly int _currentFormat;

    internal DataFile(JsonTypeInfo<T> typeInfo, int currentFormat)
    {
        _typeInfo = typeInfo;
        _currentFormat = currentFormat;
    }

    public DataFileResult<T> Read(string json)
    {
        try
        {
            using (var document = JsonDocument.Parse(json, DocumentOptions))
            {
                var root = document.RootElement;
                if (root.ValueKind != JsonValueKind.Object
                    || !root.TryGetProperty("format", out var formatElement)
                    || formatElement.ValueKind != JsonValueKind.Number
                    || !formatElement.TryGetInt32(out var format)
                    || format < 1)
                {
                    return Corrupt();
                }

                // Hier kommt später die schrittweise Migration älterer Formate hin.
                var status = format > _currentFormat ? DataFileStatus.Newer : DataFileStatus.Ok;
                var value = JsonSerializer.Deserialize(json, _typeInfo);
                return value is null ? Corrupt() : new DataFileResult<T>(value, status);
            }
        }
        catch (JsonException)
        {
            return Corrupt();
        }
    }

    public string Write(T value) => JsonSerializer.Serialize(value, _typeInfo) + "\n";

    private static DataFileResult<T> Corrupt() => new(new T(), DataFileStatus.Corrupt);

    private static readonly JsonDocumentOptions DocumentOptions = new()
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip,
    };
}
