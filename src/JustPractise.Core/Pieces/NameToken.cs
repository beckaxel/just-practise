namespace JustPractise.Core.Pieces;

/// <summary>
/// Ein Wort aus einem Dateinamen.
/// </summary>
/// <param name="Text">normalisiert: klein, ohne Apostrophe und Satzzeichen am Rand</param>
/// <param name="End">Position im Originalnamen direkt hinter dem Wort</param>
/// <param name="HardBefore">vor dem Wort steht ein hartes Trennzeichen (<c> - </c>, <c>_</c>, <c>(</c>, <c>[</c>)</param>
internal readonly record struct NameToken(string Text, int End, bool HardBefore);
