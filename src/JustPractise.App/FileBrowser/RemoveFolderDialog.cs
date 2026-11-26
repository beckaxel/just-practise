using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using JustPractise.App.Localization;

namespace JustPractise.App.FileBrowser;

/// <summary>Zeigt <see cref="RemoveFolderWindow"/> über dem gerade aktiven Fenster (Hauptfenster oder Einstellungen).</summary>
public sealed class RemoveFolderDialog(Translator translator) : IRemoveFolderDialog
{
    public async Task<RemoveFolderChoice> AskAsync(string folderName, int sidecarCount)
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime { MainWindow: { } main } lifetime)
        {
            return RemoveFolderChoice.Cancel;
        }

        var owner = lifetime.Windows.FirstOrDefault(window => window.IsActive) ?? main;
        var question = translator.Format("removeFolder.question", new Dictionary<string, object?> { ["name"] = folderName });
        var sidecars = sidecarCount > 0
            ? translator.Format("removeFolder.deleteSidecars", new Dictionary<string, object?> { ["count"] = sidecarCount })
            : null;
        var choice = await new RemoveFolderWindow(question, sidecars).ShowDialog<RemoveFolderChoice?>(owner);
        return choice ?? RemoveFolderChoice.Cancel;
    }
}
