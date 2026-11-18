using System.Collections;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using JustPractise.Core.Pieces;
using JustPractise.Core.Settings;

namespace JustPractise.Core.DataFiles;

/// <summary>Die Datendateien der App.</summary>
/// <remarks>
/// Bewusst ohne Quellgenerator: Er setzt fehlende <c>init</c>-Eigenschaften auf <c>null</c> statt auf ihren
/// Standardwert und unterstützt <c>init</c> bei <see cref="JsonExtensionDataAttribute"/> nicht.
/// </remarks>
public static class AppDataFiles
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        NewLine = "\n",
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        RespectNullableAnnotations = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,

        // Umlaute und andere Zeichen lesbar schreiben statt als ä. Die Dateien landen nie in HTML.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        TypeInfoResolver = new DefaultJsonTypeInfoResolver { Modifiers = { SkipDefaults } },
    };

    public static DataFile<SidecarFile> Sidecar { get; } = Create<SidecarFile>(SidecarFile.CurrentFormat);

    public static DataFile<AppSettings> Settings { get; } = Create<AppSettings>(AppSettings.CurrentFormat);

    public static DataFile<DeviceMemory> Device { get; } = Create<DeviceMemory>(DeviceMemory.CurrentFormat);

    private static DataFile<T> Create<T>(int currentFormat)
        where T : class, new() =>
        new((JsonTypeInfo<T>)Options.GetTypeInfo(typeof(T)), currentFormat);

    /// <summary>Nur Abweichungen speichern: leere Listen und Abschnitte mit Standardwerten weglassen.</summary>
    private static void SkipDefaults(JsonTypeInfo typeInfo)
    {
        foreach (var property in typeInfo.Properties.Where(property => !property.IsExtensionData))
        {
            property.ShouldSerialize = static (_, value) => value switch
            {
                null => false,
                IDataSection section => !section.IsDefault,
                ICollection collection => collection.Count > 0,
                _ => true,
            };
        }
    }
}
