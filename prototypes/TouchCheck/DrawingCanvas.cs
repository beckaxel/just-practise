using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace JustPractise.Prototypes.TouchCheck;

/// <summary>
/// Zeichenfläche: Jeder Zeiger (Maus, Finger, Stift) zieht eine eigene Linie. Die Strichstärke folgt dem Druck,
/// die Farbe dem Gerät (Maus grau, Finger blau, Stift orange, Radierer rot).
/// </summary>
internal sealed class DrawingCanvas : Control
{
    private readonly List<Stroke> _strokes = [];
    private readonly Dictionary<long, Stroke> _active = [];
    private readonly PointerStats _stats;
    private readonly Action<string> _status;
    private readonly Dictionary<PointerType, (long Start, int Points)> _rateWindow = [];

    public DrawingCanvas(PointerStats stats, Action<string> status)
    {
        _stats = stats;
        _status = status;
        ClipToBounds = true;
        Focusable = true;
    }

    public void Clear()
    {
        _strokes.Clear();
        _active.Clear();
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        context.FillRectangle(Brushes.White, new Rect(Bounds.Size));
        foreach (var stroke in _strokes)
        {
            for (var i = 1; i < stroke.Points.Count; i++)
            {
                var (from, _) = stroke.Points[i - 1];
                var (to, pressure) = stroke.Points[i];
                var pen = new Pen(stroke.Brush, 1 + (pressure * 14), lineCap: PenLineCap.Round);
                context.DrawLine(pen, from, to);
            }
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        var point = e.GetCurrentPoint(this);
        var stroke = new Stroke(BrushFor(e.Pointer.Type, point.Properties));
        stroke.Points.Add((point.Position, PressureOf(e.Pointer.Type, point.Properties)));
        _strokes.Add(stroke);
        _active[e.Pointer.Id] = stroke;
        e.Pointer.Capture(this);
        _stats.Pressed(e.Pointer.Type, e.Pointer.Id);
        Show(e.Pointer, point.Properties);
        InvalidateVisual();
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var points = e.GetIntermediatePoints(this);
        var last = points.Count > 0 ? points[^1] : e.GetCurrentPoint(this);
        _stats.Moved(e.Pointer.Type, last.Properties, Math.Max(points.Count, 1));
        CountRate(e.Pointer.Type, Math.Max(points.Count, 1));
        Show(e.Pointer, last.Properties);
        if (_active.TryGetValue(e.Pointer.Id, out var stroke))
        {
            foreach (var point in points)
            {
                stroke.Points.Add((point.Position, PressureOf(e.Pointer.Type, point.Properties)));
            }

            InvalidateVisual();
            e.Handled = true;
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        _active.Remove(e.Pointer.Id);
        _stats.Released(e.Pointer.Type, e.Pointer.Id);
        e.Handled = true;
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        _active.Remove(e.Pointer.Id);
        _stats.Released(e.Pointer.Type, e.Pointer.Id);
    }

    // Die Maus meldet meist 0,5; damit ihre Linie nicht dicker wirkt als ein leichter Stiftstrich, gilt sie als 0,3.
    private static float PressureOf(PointerType type, PointerPointProperties properties) =>
        type == PointerType.Mouse ? 0.3f : properties.Pressure;

    private static IBrush BrushFor(PointerType type, PointerPointProperties properties) => type switch
    {
        PointerType.Pen when properties.IsEraser || properties.IsInverted => Brushes.Red,
        PointerType.Pen => Brushes.DarkOrange,
        PointerType.Touch => Brushes.RoyalBlue,
        _ => Brushes.Gray,
    };

    private void CountRate(PointerType type, int points)
    {
        var now = Stopwatch.GetTimestamp();
        var (start, counted) = _rateWindow.GetValueOrDefault(type, (now, 0));
        counted += points;
        var seconds = Stopwatch.GetElapsedTime(start, now).TotalSeconds;
        if (seconds >= 0.5)
        {
            _stats.MoveRate(type, counted / seconds);
            _rateWindow[type] = (now, 0);
        }
        else
        {
            _rateWindow[type] = (start, counted);
        }
    }

    private void Show(IPointer pointer, PointerPointProperties p) =>
        _status($"{pointer.Type} #{pointer.Id}  Druck {p.Pressure:0.000}  Neigung {p.XTilt:0}/{p.YTilt:0}  Drehung {p.Twist:0}  " +
                $"Radierer {p.IsEraser || p.IsInverted}  Stifttaste {p.IsBarrelButtonPressed}  Finger gleichzeitig {_stats.ActiveTouches}");

    private sealed record Stroke(IBrush Brush)
    {
        public List<(Point Position, float Pressure)> Points { get; } = [];
    }
}
