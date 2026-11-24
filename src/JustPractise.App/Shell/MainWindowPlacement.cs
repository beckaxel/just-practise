using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;
using Avalonia.Threading;
using JustPractise.Core.Placement;
using JustPractise.Core.Settings;
using JustPractise.Storage;

namespace JustPractise.App.Shell;

/// <summary>
/// Stellt Größe und Position des Hauptfensters beim Start wieder her und merkt sie sich beim Beenden im
/// gerätespezifischen Gedächtnis. Das Fenster ist beim Start immer vollständig sichtbar (<see cref="WindowPlacer"/>).
/// </summary>
/// <remarks>
/// Gemerkt wird die Lage im normalen Zustand. War das Fenster maximiert, erscheint es wieder maximiert, und beim
/// Aufheben der Maximierung kehrt es an die gemerkte Stelle zurück.
/// </remarks>
internal sealed class MainWindowPlacement(DataFileKeeper<DeviceMemory> memory)
{
    private readonly WindowPlacer _placer = new(defaultWidth: 1000, defaultHeight: 700);
    private WindowPlacement? _normal;
    private bool _maximized;

    /// <summary>Vor dem ersten Anzeigen aufrufen.</summary>
    public void Attach(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);
        Restore(window);

        // Avalonia meldet Position, Größe und Zustand beim Maximieren nicht immer in derselben Reihenfolge.
        // Erst nachdem alle Meldungen verarbeitet sind, ist klar, ob das Fenster gerade normal ist.
        void Track() => Dispatcher.UIThread.Post(() => Remember(window), DispatcherPriority.Background);
        window.PositionChanged += (_, _) => Track();
        window.Resized += (_, _) => Track();
        window.PropertyChanged += (_, e) =>
        {
            if (e.Property == Window.WindowStateProperty)
            {
                Track();
            }
        };
        window.Closing += (_, _) => Save(window);
    }

    private void Restore(Window window)
    {
        var screens = window.Screens;
        var placement = _placer.Place(
            memory.Value.Window,
            [.. screens.All.Select(ToArea)],
            screens.Primary is { } primary ? ToArea(primary) : null);
        if (placement is null)
        {
            return;
        }

        window.WindowStartupLocation = WindowStartupLocation.Manual;
        window.Position = new PixelPoint(placement.X, placement.Y);
        window.Width = placement.Width;
        window.Height = placement.Height;
        if (placement.Maximized)
        {
            window.WindowState = WindowState.Maximized;
        }

        _normal = placement with { Maximized = false };
        _maximized = placement.Maximized;
    }

    private void Remember(Window window)
    {
        switch (window.WindowState)
        {
            case WindowState.Normal:
                _normal = new WindowPlacement
                {
                    X = window.Position.X,
                    Y = window.Position.Y,
                    Width = (int)Math.Round(window.ClientSize.Width),
                    Height = (int)Math.Round(window.ClientSize.Height),
                };
                _maximized = false;
                break;
            case WindowState.Maximized:
                _maximized = true;
                break;
            default:
                // Minimiert oder Vollbild: Es bleibt der Zustand davor.
                break;
        }
    }

    private void Save(Window window)
    {
        Remember(window);
        if (_normal is not { } normal)
        {
            return;
        }

        memory.Update(value =>
        {
            var placement = normal with { Maximized = _maximized, Unknown = value.Window?.Unknown };
            return value.Window == placement ? value : value with { Window = placement };
        });
    }

    private static ScreenArea ToArea(Screen screen) => new(
        new PixelBox(screen.WorkingArea.X, screen.WorkingArea.Y, screen.WorkingArea.Width, screen.WorkingArea.Height),
        screen.Scaling);
}
