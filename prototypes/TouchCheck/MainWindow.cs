using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.GestureRecognizers;
using Avalonia.Layout;
using Avalonia.Media;

namespace JustPractise.Prototypes.TouchCheck;

/// <summary>Links die Zeichenfläche, rechts Bedienelemente wie in der App, unten Status und Bericht.</summary>
internal sealed class MainWindow : Window
{
    private readonly PointerStats _stats = new();
    private readonly TextBlock _status = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(8) };
    private readonly TextBlock _counters = new() { TextWrapping = TextWrapping.Wrap, FontSize = 12 };

    public MainWindow()
    {
        Title = "TouchCheck – Prüfpunkt 6";
        Width = 1200;
        Height = 800;
        WindowState = WindowState.Maximized;

        var canvas = new DrawingCanvas(_stats, text => _status.Text = text);

        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,380"),
            RowDefinitions = new RowDefinitions("*,Auto"),
        };
        grid.Children.Add(canvas);
        var panel = new ScrollViewer { Content = Controls(), Padding = new Thickness(12) };
        Grid.SetColumn(panel, 1);
        grid.Children.Add(panel);

        var clear = new Button { Content = "Zeichnung löschen", Margin = new Thickness(8) };
        clear.Click += (_, _) => canvas.Clear();
        var save = new Button { Content = "Bericht speichern", Margin = new Thickness(8) };
        save.Click += (_, _) => _status.Text = "Gespeichert: " + SaveReport();
        var bottom = new DockPanel();
        DockPanel.SetDock(clear, Dock.Right);
        DockPanel.SetDock(save, Dock.Right);
        bottom.Children.Add(save);
        bottom.Children.Add(clear);
        bottom.Children.Add(_status);
        Grid.SetRow(bottom, 1);
        Grid.SetColumnSpan(bottom, 2);
        grid.Children.Add(bottom);

        Content = grid;
        _status.Text = $"{Environment()}  –  links mit Finger, Stift oder Maus zeichnen, rechts die Bedienelemente ausprobieren.";
        UpdateCounters();
    }

    private StackPanel Controls()
    {
        var panel = new StackPanel { Spacing = 14 };
        panel.Children.Add(new TextBlock { Text = "Bedienelemente", FontSize = 18, FontWeight = FontWeight.SemiBold });

        var big = new Button { Content = "Großer Knopf (48 px)", MinHeight = 48, HorizontalAlignment = HorizontalAlignment.Stretch };
        big.Click += (_, _) => Count("Knopf groß getippt");
        panel.Children.Add(big);

        var small = new Button { Content = "klein", MinHeight = 24, Height = 24, Padding = new Thickness(6, 0), FontSize = 11 };
        small.Click += (_, _) => Count("Knopf klein (24 px) getippt");
        panel.Children.Add(small);

        var sliderLabel = new TextBlock { Text = "Schieberegler: 50" };
        var slider = new Slider { Minimum = 0, Maximum = 100, Value = 50 };
        slider.ValueChanged += (_, e) =>
        {
            sliderLabel.Text = string.Create(CultureInfo.InvariantCulture, $"Schieberegler: {e.NewValue:0}");
            Count("Schieberegler bewegt (Wertänderungen)");
        };
        panel.Children.Add(sliderLabel);
        panel.Children.Add(slider);

        var toggle = new ToggleSwitch { Content = "Schalter" };
        toggle.IsCheckedChanged += (_, _) => Count("Schalter umgelegt");
        panel.Children.Add(toggle);

        panel.Children.Add(new TextBlock { Text = "Liste: mit dem Finger wischen (mit Schwung)" });
        var items = new StackPanel();
        for (var i = 1; i <= 100; i++)
        {
            items.Children.Add(new TextBlock { Text = $"Stück {i}", Padding = new Thickness(8, 10) });
        }

        var list = new ScrollViewer { Content = items, Height = 200, Background = Brushes.WhiteSmoke };
        var scrolled = false;
        list.ScrollChanged += (_, _) =>
        {
            if (!scrolled)
            {
                Count("Liste gescrollt (Gesten)");
            }

            scrolled = true;
        };
        list.PointerReleased += (_, _) => scrolled = false;
        list.PointerCaptureLost += (_, _) => scrolled = false;
        panel.Children.Add(list);

        var scaleLabel = new TextBlock { Text = "Zwei Finger: zoomen (Pinch)", HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        var pinch = new Border { Height = 140, Background = Brushes.LightSteelBlue, Child = scaleLabel };
        pinch.GestureRecognizers.Add(new PinchGestureRecognizer());
        pinch.AddHandler(InputElement.PinchEvent, (_, e) =>
        {
            scaleLabel.Text = string.Create(CultureInfo.InvariantCulture, $"Pinch: Faktor {e.Scale:0.00}");
            Count("Pinch-Ereignisse");
        });
        pinch.AddHandler(InputElement.PinchEndedEvent, (_, _) => Count("Pinch beendet"));
        panel.Children.Add(pinch);

        var press = new Border
        {
            Height = 100,
            Background = Brushes.Khaki,
            Child = new TextBlock { Text = "Tippen, doppelt tippen, lange drücken", HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center },
            ContextMenu = new ContextMenu { ItemsSource = new[] { "Kontextmenü (Rechtsklick oder lange drücken)" } },
        };
        press.Tapped += (_, _) => Count("Tippen");
        press.DoubleTapped += (_, _) => Count("Doppelt tippen");
        press.Holding += (_, e) => Count($"Lange drücken ({e.HoldingState})");
        press.ContextRequested += (_, _) => Count("Kontextmenü angefordert");
        panel.Children.Add(press);

        panel.Children.Add(new TextBlock { Text = "Zähler", FontSize = 16, FontWeight = FontWeight.SemiBold });
        panel.Children.Add(_counters);
        return panel;
    }

    private void Count(string name)
    {
        _stats.Gesture(name);
        UpdateCounters();
    }

    private void UpdateCounters() => _counters.Text = _stats.Report(Environment());

    private string SaveReport()
    {
        var directory = Path.GetFullPath("touchcheck-out");
        Directory.CreateDirectory(directory);
        var os = OperatingSystem.IsWindows() ? "win" : OperatingSystem.IsMacOS() ? "mac" : "linux";
        var session = System.Environment.GetEnvironmentVariable("XDG_SESSION_TYPE");
        var path = Path.Combine(directory, $"report-{os}{(session is { Length: > 0 } ? "-" + session : "")}-{DateTime.Now:HHmmss}.txt");
        File.WriteAllText(path, _stats.Report(Environment()));
        return path;
    }

    private static string Environment()
    {
        var session = System.Environment.GetEnvironmentVariable("XDG_SESSION_TYPE");
        var desktop = System.Environment.GetEnvironmentVariable("XDG_CURRENT_DESKTOP");
        var linux = OperatingSystem.IsLinux()
            ? $", Sitzung {session ?? "?"} ({desktop ?? "?"}), Avalonia über X11{(session == "wayland" ? " (XWayland)" : "")}"
            : string.Empty;
        return $"{System.Environment.OSVersion}{linux}, .NET {System.Environment.Version}";
    }
}
