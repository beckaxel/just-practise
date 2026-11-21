using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace JustPractise.Prototypes.GcCheck;

/// <summary>Zeichnet in jedem Frame viele bewegte Linien, damit Layout und Rendering von Avalonia Last erzeugen.</summary>
internal sealed class RenderLoad : Control
{
    private static readonly IPen Pen = new ImmutablePen(Brushes.SteelBlue, 1);

    private double _phase;

    public int Lines { get; set; } = 2000;

    public void Advance(TimeSpan elapsed)
    {
        _phase += elapsed.TotalSeconds;
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        var width = Bounds.Width;
        var height = Bounds.Height;
        if (width <= 0 || height <= 0 || Lines <= 0)
        {
            return;
        }

        var step = width / Lines;
        for (var i = 0; i < Lines; i++)
        {
            var x = i * step;
            var amplitude = Math.Sin((_phase * 3) + (i * 0.05)) * Math.Cos((_phase * 0.7) + (i * 0.013));
            var y = height / 2 * (1 - amplitude);
            context.DrawLine(Pen, new Point(x, height / 2), new Point(x, y));
        }
    }
}
