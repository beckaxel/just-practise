namespace JustPractise.Core.Placement;

/// <summary>Ein Rechteck in Bildschirmpixeln.</summary>
public readonly record struct PixelBox(int X, int Y, int Width, int Height)
{
    public int Right => X + Width;

    public int Bottom => Y + Height;

    public bool Contains(PixelBox other) =>
        other.X >= X && other.Y >= Y && other.Right <= Right && other.Bottom <= Bottom;

    /// <summary>Fläche der Überschneidung in Pixeln, 0 ohne Überschneidung.</summary>
    public long Overlap(PixelBox other)
    {
        long width = Math.Min(Right, other.Right) - Math.Max(X, other.X);
        long height = Math.Min(Bottom, other.Bottom) - Math.Max(Y, other.Y);
        return width > 0 && height > 0 ? width * height : 0;
    }
}

/// <summary>Ein Monitor: der Arbeitsbereich ohne Taskleiste in Pixeln und seine Skalierung.</summary>
public readonly record struct ScreenArea(PixelBox WorkingArea, double Scaling);

/// <summary>
/// Legt fest, wo das Hauptfenster beim Start erscheint. Es ist danach immer vollständig sichtbar, auch wenn sich die
/// Monitore seit dem letzten Lauf geändert haben.
/// </summary>
/// <remarks>
/// Wie groß Rahmen und Titelleiste sind, weiß die App vor dem Öffnen nicht; das hängt vom System ab. Gerechnet wird
/// deshalb mit einem großzügigen Zuschlag (<see cref="Frame"/>) zur Größe des Inhalts.
/// </remarks>
public sealed class WindowPlacer(double defaultWidth, double defaultHeight)
{
    /// <summary>Zuschlag für Rahmen und Titelleiste in geräteunabhängigen Einheiten (Breite, Höhe).</summary>
    public static (int Width, int Height) Frame { get; } = (16, 48);

    /// <summary>
    /// Die Lage für diesen Start. <paramref name="saved"/> wird unverändert übernommen, wenn das Fenster damit ganz
    /// auf einem Monitor liegt. Liegt es nur teilweise auf einem Monitor, wird es auf diesen geschoben und bei Bedarf
    /// verkleinert. Liegt es auf keinem (z. B. weil der Monitor fehlt), oder gibt es noch keine gemerkte Lage,
    /// erscheint es mittig auf dem Hauptmonitor.
    /// </summary>
    /// <param name="screens">Alle Monitore; leer, wenn das System keine meldet.</param>
    /// <param name="primary">Der Hauptmonitor, sonst der erste aus <paramref name="screens"/>.</param>
    /// <returns><c>null</c>, wenn es keine Monitore gibt; dann bestimmt das System.</returns>
    public WindowPlacement? Place(WindowPlacement? saved, IReadOnlyList<ScreenArea> screens, ScreenArea? primary)
    {
        ArgumentNullException.ThrowIfNull(screens);
        if (screens.Count == 0)
        {
            return null;
        }

        var main = primary ?? screens[0];
        if (saved is null || saved.Width <= 0 || saved.Height <= 0)
        {
            return Center(main, defaultWidth, defaultHeight, maximized: false);
        }

        if (screens.Any(screen => screen.WorkingArea.Contains(OuterBox(saved, screen.Scaling))))
        {
            return saved;
        }

        var best = screens.MaxBy(screen => screen.WorkingArea.Overlap(OuterBox(saved, screen.Scaling)));
        if (best.WorkingArea.Overlap(OuterBox(saved, best.Scaling)) == 0)
        {
            return Center(main, saved.Width, saved.Height, saved.Maximized) with { FullScreen = saved.FullScreen, Unknown = saved.Unknown };
        }

        var (width, height) = FitSize(best, saved.Width, saved.Height);
        var box = OuterBox(saved with { Width = width, Height = height }, best.Scaling);
        var area = best.WorkingArea;
        return saved with
        {
            X = Math.Clamp(box.X, area.X, Math.Max(area.X, area.Right - box.Width)),
            Y = Math.Clamp(box.Y, area.Y, Math.Max(area.Y, area.Bottom - box.Height)),
            Width = width,
            Height = height,
        };
    }

    /// <summary>Das Fenster samt Zuschlag für Rahmen und Titelleiste in Pixeln.</summary>
    private static PixelBox OuterBox(WindowPlacement placement, double scaling) => new(
        placement.X,
        placement.Y,
        ToPixels(placement.Width + Frame.Width, scaling),
        ToPixels(placement.Height + Frame.Height, scaling));

    private static WindowPlacement Center(ScreenArea screen, double width, double height, bool maximized)
    {
        (width, height) = FitSize(screen, width, height);
        var area = screen.WorkingArea;
        var outerWidth = ToPixels(width + Frame.Width, screen.Scaling);
        var outerHeight = ToPixels(height + Frame.Height, screen.Scaling);
        return new WindowPlacement
        {
            X = area.X + Math.Max(0, (area.Width - outerWidth) / 2),
            Y = area.Y + Math.Max(0, (area.Height - outerHeight) / 2),
            Width = width,
            Height = height,
            Maximized = maximized,
        };
    }

    /// <summary>Verkleinert die Größe, bis das Fenster samt Rahmen in den Arbeitsbereich passt.</summary>
    private static (double Width, double Height) FitSize(ScreenArea screen, double width, double height)
    {
        var scaling = screen.Scaling > 0 ? screen.Scaling : 1;
        var maxWidth = (int)Math.Floor(screen.WorkingArea.Width / scaling) - Frame.Width;
        var maxHeight = (int)Math.Floor(screen.WorkingArea.Height / scaling) - Frame.Height;
        return (Math.Max(1, Math.Min(width, maxWidth)), Math.Max(1, Math.Min(height, maxHeight)));
    }

    private static int ToPixels(double units, double scaling) =>
        (int)Math.Ceiling(units * (scaling > 0 ? scaling : 1));
}
