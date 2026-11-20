using CommunityToolkit.Mvvm.ComponentModel;
using JustPractise.App.FileBrowser;

namespace JustPractise.App.Shell;

/// <summary>Hält die ViewModels der Bereiche zusammen: Dateibrowser, Player und Metronom.</summary>
public sealed partial class MainWindowViewModel(FileBrowserViewModel browser) : ObservableObject
{
    public FileBrowserViewModel Browser { get; } = browser;
}
