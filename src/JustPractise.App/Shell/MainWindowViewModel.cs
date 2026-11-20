using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using JustPractise.App.FileBrowser;
using JustPractise.App.Player;

namespace JustPractise.App.Shell;

/// <summary>Hält die ViewModels der Bereiche zusammen: Dateibrowser, Player und Metronom.</summary>
public sealed partial class MainWindowViewModel : ObservableObject
{
    public MainWindowViewModel(FileBrowserViewModel browser, PlayerViewModel player)
    {
        Browser = browser;
        Player = player;
        Browser.PropertyChanged += OnBrowserChanged;
    }

    public FileBrowserViewModel Browser { get; }

    public PlayerViewModel Player { get; }

    /// <summary>Hält die Anzeigen aktuell; die Oberfläche ruft das etwa 30-mal pro Sekunde auf.</summary>
    public void Tick() => Player.Tick();

    /// <summary>Eine im Browser gewählte Datei lädt der Player.</summary>
    private void OnBrowserChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(FileBrowserViewModel.SelectedFile) && Browser.SelectedFile is { } file)
        {
            _ = Player.OpenAsync(file);
        }
    }
}
