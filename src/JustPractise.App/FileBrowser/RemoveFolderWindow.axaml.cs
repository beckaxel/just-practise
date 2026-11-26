using Avalonia.Controls;
using Avalonia.Interactivity;

namespace JustPractise.App.FileBrowser;

/// <summary>Fragt vor dem Entfernen eines Musikordners nach und bietet an, die Sidecar-Dateien mitzulöschen.</summary>
public sealed partial class RemoveFolderWindow : Window
{
    public RemoveFolderWindow() => InitializeComponent();

    public RemoveFolderWindow(string question, string? deleteSidecarsText)
        : this()
    {
        Question.Text = question;
        SidecarPanel.IsVisible = deleteSidecarsText is not null;
        DeleteSidecarsText.Text = deleteSidecarsText;
    }

    private void OnCancel(object? sender, RoutedEventArgs e) => Close(RemoveFolderChoice.Cancel);

    private void OnRemove(object? sender, RoutedEventArgs e) =>
        Close(DeleteSidecars.IsChecked == true ? RemoveFolderChoice.RemoveWithSidecars : RemoveFolderChoice.Remove);
}
