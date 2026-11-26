using System.Text.Json;
using System.Text.Json.Serialization;

namespace JustPractise.Core.Placement;

/// <summary>
/// Die zuletzt gemerkte Lage des Hauptfensters in <c>device.json</c>. Position und Größe gelten immer für den
/// normalen Zustand, auch wenn das Fenster maximiert war: Dorthin kehrt es zurück, wenn man die Maximierung aufhebt.
/// </summary>
public sealed record WindowPlacement
{
    /// <summary>Linke obere Ecke des Fensters in Bildschirmpixeln (kann bei mehreren Monitoren negativ sein).</summary>
    public int X { get; init; }

    public int Y { get; init; }

    /// <summary>
    /// Breite des Fensterinhalts in geräteunabhängigen Einheiten (ohne Rahmen und Titelleiste), auf zwei
    /// Nachkommastellen. Ganze Zahlen reichen nicht: Bei krummer Skalierung (z. B. 104 %) würde das Fenster sonst
    /// bei jedem Start um eine Einheit schrumpfen.
    /// </summary>
    public double Width { get; init; }

    public double Height { get; init; }

    public bool Maximized { get; init; }

    /// <summary>
    /// War das Fenster im Vollbild; dann startet es wieder so. <see cref="Maximized"/> sagt, wohin es beim Verlassen
    /// des Vollbilds zurückkehrt.
    /// </summary>
    public bool FullScreen { get; init; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Unknown { get; init; }
}
