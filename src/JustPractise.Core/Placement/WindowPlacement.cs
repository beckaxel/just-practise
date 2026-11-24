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

    /// <summary>Breite des Fensterinhalts in geräteunabhängigen Einheiten (ohne Rahmen und Titelleiste).</summary>
    public int Width { get; init; }

    public int Height { get; init; }

    public bool Maximized { get; init; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Unknown { get; init; }
}
