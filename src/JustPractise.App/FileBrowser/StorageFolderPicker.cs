using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using JustPractise.App.Localization;

namespace JustPractise.App.FileBrowser;

/// <summary>Öffnet den Ordnerdialog des Systems über das Hauptfenster.</summary>
public sealed class StorageFolderPicker(Translator translator) : IFolderPicker
{
    public async Task<string?> PickFolderAsync()
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime { MainWindow: TopLevel window })
        {
            return null;
        }

        var folders = await window.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = translator.Get("browser.chooseFolder"),
            AllowMultiple = false,
        });

        // Nur echte Ordner im Dateisystem; virtuelle Orte (z. B. Portale ohne Pfad) haben keinen lokalen Pfad.
        return folders.Count > 0 ? folders[0].TryGetLocalPath() : null;
    }
}
