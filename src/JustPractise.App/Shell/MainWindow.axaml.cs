using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using JustPractise.App.Shortcuts;

namespace JustPractise.App.Shell;

public sealed partial class MainWindow : Window
{
    private SettingsWindow? _settings;
    private WindowState _beforeMinimized = WindowState.Normal;
    private WindowState _beforeFullScreen = WindowState.Normal;

    public MainWindow()
    {
        InitializeComponent();

        // Vor den Bedienelementen abfangen: Ein Kürzel wirkt immer gleich, egal was den Fokus hat
        // (sonst würde z. B. die Leertaste den fokussierten Knopf auslösen).
        AddHandler(KeyDownEvent, OnPreviewKeyDown, RoutingStrategies.Tunnel);
        PropertyChanged += (_, e) =>
        {
            if (e.Property == WindowStateProperty && e.OldValue is WindowState old and not WindowState.Minimized)
            {
                _beforeMinimized = old;
                if (e.NewValue is WindowState.FullScreen && old is not WindowState.FullScreen)
                {
                    _beforeFullScreen = old;
                }
            }
        };
        DataContextChanged += (_, _) =>
        {
            if (DataContext is MainWindowViewModel viewModel)
            {
                viewModel.SettingsRequested += (_, _) => ShowSettings(viewModel);
                viewModel.FullScreenToggleRequested += (_, _) => ToggleFullScreen();
                AttachLibraryPane(viewModel.LibraryPane);
                viewModel.PropertyChanged += (_, e) =>
                {
                    if (e.PropertyName == nameof(MainWindowViewModel.IsMetronomeOpen) && viewModel.IsMetronomeOpen)
                    {
                        PlaceMetronomePanel();
                    }
                };
                Dismiss.PointerPressed += (_, e) =>
                {
                    // Ein Tipp neben die Einstellungen schließt sie; trifft er Start/Stopp, startet bzw. stoppt
                    // das Metronom zugleich (der Knopf liegt unter dieser Ebene und bekommt den Tipp sonst nicht).
                    viewModel.IsMetronomeOpen = false;
                    var start = MetronomeTool.StartStop;
                    if (new Rect(start.Bounds.Size).Contains(e.GetPosition(start)))
                    {
                        viewModel.Metronome.StartStop();
                    }

                    e.Handled = true;
                };
            }
        };
    }

    /// <summary>Wechselt ins Vollbild oder zurück in den Zustand davor (normal oder maximiert).</summary>
    public void ToggleFullScreen() =>
        WindowState = WindowState == WindowState.FullScreen ? _beforeFullScreen : WindowState.FullScreen;

    /// <summary>Holt das Fenster nach vorn, z. B. wenn die App ein zweites Mal gestartet wurde.</summary>
    public void BringToFront()
    {
        if (WindowState == WindowState.Minimized)
        {
            WindowState = _beforeMinimized;
        }

        Activate();
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

        if (e.Key == Key.Escape && e.KeyModifiers == KeyModifiers.None)
        {
            if (viewModel.IsMetronomeOpen)
            {
                viewModel.IsMetronomeOpen = false;
                e.Handled = true;
                return;
            }

            if (WindowState == WindowState.FullScreen)
            {
                ToggleFullScreen();
                e.Handled = true;
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

    /// <summary>
    /// Setzt die Breite der Bibliothek aus <see cref="LibraryPane"/> und merkt sich, was der Nutzer am Trenner zieht.
    /// </summary>
    private void AttachLibraryPane(LibraryPane pane)
    {
        void Apply()
        {
            var column = Body.ColumnDefinitions[0];
            column.MinWidth = pane.IsCollapsed ? LibraryPane.CollapsedWidth : LibraryPane.MinWidth;
            column.MaxWidth = pane.IsCollapsed ? LibraryPane.CollapsedWidth : LibraryPane.MaxWidth;
            column.Width = new GridLength(pane.IsCollapsed ? LibraryPane.CollapsedWidth : pane.Width);
        }

        Apply();
        pane.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(LibraryPane.IsCollapsed))
            {
                Apply();
            }
        };
        Splitter.DragCompleted += (_, _) => pane.Resized(Body.ColumnDefinitions[0].ActualWidth);
    }

    /// <summary>Legt die Einstellungen des Metronoms rechtsbündig direkt unter das Werkzeug.</summary>
    private void PlaceMetronomePanel()
    {
        if (MetronomeTool.TranslatePoint(new Point(MetronomeTool.Bounds.Width, MetronomeTool.Bounds.Height), Root) is { } corner)
        {
            MetronomePanel.Margin = new Thickness(0, corner.Y + 12, Math.Max(8, Root.Bounds.Width - corner.X - 12), 0);
        }
    }
}
