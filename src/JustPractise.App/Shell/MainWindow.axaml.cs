using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using JustPractise.App.Shortcuts;

namespace JustPractise.App.Shell;

public sealed partial class MainWindow : Window
{
    private SettingsWindow? _settings;

    public MainWindow()
    {
        InitializeComponent();

        // Vor den Bedienelementen abfangen: Ein Kürzel wirkt immer gleich, egal was den Fokus hat
        // (sonst würde z. B. die Leertaste den fokussierten Knopf auslösen).
        AddHandler(KeyDownEvent, OnPreviewKeyDown, RoutingStrategies.Tunnel);
        DataContextChanged += (_, _) =>
        {
            if (DataContext is MainWindowViewModel viewModel)
            {
                viewModel.SettingsRequested += (_, _) => ShowSettings(viewModel);
            }
        };
    }

    private void OnPreviewKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        if (FocusManager?.GetFocusedElement() is TextBox)
        {
            if (e.Key == Key.Escape)
            {
                // Esc verlässt das Textfeld; danach wirken die Kürzel wieder.
                Focus();
                e.Handled = true;
                return;
            }

            if (!Shortcut.WorksInTextInput(e.KeyModifiers))
            {
                return;
            }
        }

        if (Shortcut.Match(e.Key, e.KeyModifiers, e.KeySymbol) is { } action)
        {
            viewModel.Execute(action);
            e.Handled = true;
        }
    }

    private void ShowSettings(MainWindowViewModel viewModel)
    {
        if (_settings is not null)
        {
            _settings.Activate();
            return;
        }

        _settings = new SettingsWindow { DataContext = viewModel.Settings };
        _settings.Closed += (_, _) => _settings = null;
        _ = _settings.ShowDialog(this);
    }
}
