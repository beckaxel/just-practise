namespace JustPractise.Core.DataFiles;

/// <summary>
/// Ein Abschnitt einer Datendatei. Steht er auf Standardwerten, wird er nicht geschrieben
/// (Regel „nur Abweichungen speichern“).
/// </summary>
public interface IDataSection
{
    bool IsDefault { get; }
}
