using Avalonia.Controls;

namespace JustPractise.App.Shell;

public sealed partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (DataContext is SettingsViewModel viewModel)
            {
                viewModel.CloseRequested += OnCloseRequested;
                Closed += (_, _) => viewModel.CloseRequested -= OnCloseRequested;
            }
        };
    }

    private void OnCloseRequested(object? sender, EventArgs e) => Close();
}
