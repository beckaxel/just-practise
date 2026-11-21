using JustPractise.Core.DataFiles;
using JustPractise.Core.Pieces;
using JustPractise.Core.Settings;

namespace JustPractise.Storage;

/// <summary>
/// Wo die Datendateien liegen: Einstellungen und Gedächtnis im Anwendungsdatenordner,
/// Sidecar-Dateien neben den Stücken.
/// </summary>
public sealed class AppDataLocation(string directory)
{
    public const string AppFolderName = "just-practise";

    /// <summary>
    /// Der Anwendungsdatenordner des Systems: Linux <c>~/.config/just-practise</c> (bzw. <c>$XDG_CONFIG_HOME</c>),
    /// Windows <c>%APPDATA%\just-practise</c>, macOS <c>~/Library/Application Support/just-practise</c>.
    /// </summary>
    public static AppDataLocation System { get; } = new(Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData, Environment.SpecialFolderOption.DoNotVerify),
        AppFolderName));

    public string Directory { get; } = directory;

    /// <summary>Eigene Sprachdateien, die ohne Neubau der App geladen werden.</summary>
    public string TranslationsDirectory => Path.Combine(Directory, "translations");

    public DataFileStore<AppSettings> Settings() => new(Path.Combine(Directory, "settings.json"), AppDataFiles.Settings);

    public DataFileStore<DeviceMemory> Device() => new(Path.Combine(Directory, "device.json"), AppDataFiles.Device);

    public static DataFileStore<SidecarFile> Sidecar(string folder, string pieceTitle) =>
        new(Path.Combine(folder, SidecarFile.FileNameFor(pieceTitle)), AppDataFiles.Sidecar);
}
