using Avalonia.Controls;
using Avalonia.Threading;

namespace JustPractise.App.FileBrowser;

public sealed partial class FileBrowserView : UserControl
{
    private FileBrowserViewModel? _viewModel;

    public FileBrowserView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (_viewModel is not null)
            {
                _viewModel.ScrollRequested -= OnScrollRequested;
            }

            _viewModel = DataContext as FileBrowserViewModel;
            if (_viewModel is not null)
            {
                _viewModel.ScrollRequested += OnScrollRequested;
            }
        };
    }

    /// <summary>Erst nach dem Layout scrollen, sonst gibt es die Zeile noch nicht.</summary>
    private void OnScrollRequested(object? sender, LibraryRow row) =>
        Dispatcher.UIThread.Post(() => Library.ScrollIntoView(row), DispatcherPriority.Loaded);
}
