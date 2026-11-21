namespace JustPractise.Core.Pieces;

/// <summary>
/// Ein Stück in einem Ordner: der angezeigte Titel und die Dateien, die dazugehören.
/// </summary>
public sealed record Piece(string Title, IReadOnlyList<string> Files);
