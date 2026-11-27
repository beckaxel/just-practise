using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Media;
using JustPractise.Core.Waveform;

namespace JustPractise.App.Player;

/// <summary>
/// Zeichnet die Wellenform, die Abspielposition und den sichtbaren Ausschnitt; auch mit dem Finger bedienbar.
/// </summary>
/// <remarks>
/// <para>
/// Groß (<see cref="IsOverview"/> aus) zeigt sie den <see cref="Viewport"/>: Ein Tipp springt an die Stelle, Ziehen
/// verschiebt den Ausschnitt, zwei Finger oder Strg+Mausrad zoomen um die Stelle darunter, das Mausrad allein verschiebt.
/// Als Übersicht zeigt sie das ganze Stück mit dem Ausschnitt darin: Tippen oder Ziehen holt die Stelle in die Mitte des
/// Ausschnitts.
/// </para>
/// <para>
/// Die Form der Wellenform wird nur neu berechnet, wenn sich Ausschnitt, Größe oder Spitzen ändern; die
/// Abspielposition ändert nur, wo die Farbe wechselt.
/// </para>
/// </remarks>
public sealed class WaveformControl : Control
{
    public static readonly StyledProperty<WaveformPeaks?> PeaksProperty =
        AvaloniaProperty.Register<WaveformControl, WaveformPeaks?>(nameof(Peaks));

    public static readonly StyledProperty<WaveformViewport> ViewportProperty =
        AvaloniaProperty.Register<WaveformControl, WaveformViewport>(nameof(Viewport), defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<double> PositionProperty =
        AvaloniaProperty.Register<WaveformControl, double>(nameof(Position), defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<bool> IsOverviewProperty =
        AvaloniaProperty.Register<WaveformControl, bool>(nameof(IsOverview));

    public static readonly StyledProperty<IBrush?> BackgroundProperty =
        AvaloniaProperty.Register<WaveformControl, IBrush?>(nameof(Background));

    public static readonly StyledProperty<IBrush?> WaveBrushProperty =
        AvaloniaProperty.Register<WaveformControl, IBrush?>(nameof(WaveBrush), Brushes.Gray);

    public static readonly StyledProperty<IBrush?> PlayedBrushProperty =
        AvaloniaProperty.Register<WaveformControl, IBrush?>(nameof(PlayedBrush), Brushes.SteelBlue);

    public static readonly StyledProperty<IBrush?> PositionBrushProperty =
        AvaloniaProperty.Register<WaveformControl, IBrush?>(nameof(PositionBrush), Brushes.White);

    public static readonly StyledProperty<IBrush?> WindowBrushProperty =
        AvaloniaProperty.Register<WaveformControl, IBrush?>(nameof(WindowBrush));

    /// <summary>Ab so viel Bewegung ist ein Druck kein Tipp mehr, sondern Ziehen.</summary>
    private const double DragThreshold = 8;

    /// <summary>Zoom pro Raste des Mausrads mit Strg.</summary>
    private const double WheelZoom = 1.25;

    /// <summary>Verschiebung pro Raste des Mausrads, als Anteil des Ausschnitts.</summary>
    private const double WheelPan = 0.1;

    private readonly Dictionary<IPointer, Point> _pointers = [];
    private Point _pressPoint;
    private WaveformViewport _gestureViewport;
    private bool _dragging;
    private bool _pinching;
    private bool _gestureUsed;
    private double _pinchDistance;
    private double _pinchAnchor;

    private float[] _min = [];
    private float[] _max = [];
    private StreamGeometry? _shape;
    private (WaveformPeaks? Peaks, double Start, double Length, Size Size) _shapeKey;

    static WaveformControl()
    {
        AffectsRender<WaveformControl>(
            PeaksProperty, ViewportProperty, PositionProperty, IsOverviewProperty, BackgroundProperty,
            WaveBrushProperty, PlayedBrushProperty, PositionBrushProperty, WindowBrushProperty);
        ClipToBoundsProperty.OverrideDefaultValue<WaveformControl>(true);
        FocusableProperty.OverrideDefaultValue<WaveformControl>(false);
    }

    public WaveformPeaks? Peaks
    {
        get => GetValue(PeaksProperty);
        set => SetValue(PeaksProperty, value);
    }

    public WaveformViewport Viewport
    {
        get => GetValue(ViewportProperty);
        set => SetValue(ViewportProperty, value);
    }

    /// <summary>Die Abspielposition in Sekunden. Ein Tipp in die große Wellenform setzt sie.</summary>
    public double Position
    {
        get => GetValue(PositionProperty);
        set => SetValue(PositionProperty, value);
    }

    public bool IsOverview
    {
        get => GetValue(IsOverviewProperty);
        set => SetValue(IsOverviewProperty, value);
    }

    public IBrush? Background
    {
        get => GetValue(BackgroundProperty);
        set => SetValue(BackgroundProperty, value);
    }

    public IBrush? WaveBrush
    {
        get => GetValue(WaveBrushProperty);
        set => SetValue(WaveBrushProperty, value);
    }

    /// <summary>Der schon gespielte Teil links der Position.</summary>
    public IBrush? PlayedBrush
    {
        get => GetValue(PlayedBrushProperty);
        set => SetValue(PlayedBrushProperty, value);
    }

    public IBrush? PositionBrush
    {
        get => GetValue(PositionBrushProperty);
        set => SetValue(PositionBrushProperty, value);
    }

    /// <summary>Der sichtbare Ausschnitt in der Übersicht.</summary>
    public IBrush? WindowBrush
    {
        get => GetValue(WindowBrushProperty);
        set => SetValue(WindowBrushProperty, value);
    }

    /// <summary>Der gezeichnete Zeitbereich: der Ausschnitt oder, als Übersicht, das ganze Stück.</summary>
    private (double Start, double Length) Shown =>
        IsOverview ? (0, Viewport.Duration) : (Viewport.Start, Viewport.Length);

    public override void Render(DrawingContext context)
    {
        var size = Bounds.Size;
        var rect = new Rect(size);
        // Auch ohne Hintergrund überall treffbar, damit Tippen und Ziehen neben der Kurve wirken.
        context.FillRectangle(Background ?? Brushes.Transparent, rect);

        var (start, length) = Shown;
        if (size.Width <= 0 || size.Height <= 0)
        {
            return;
        }

        var middle = size.Height / 2;
        var positionX = length > 0 ? (Position - start) / length * size.Width : 0;
        var shape = Shape(size, start, length);
        if (shape is not null)
        {
            using (context.PushClip(new Rect(0, 0, Math.Clamp(positionX, 0, size.Width), size.Height)))
            {
                context.DrawGeometry(PlayedBrush, null, shape);
            }

            using (context.PushClip(new Rect(Math.Clamp(positionX, 0, size.Width), 0, size.Width, size.Height)))
            {
                context.DrawGeometry(WaveBrush, null, shape);
            }
        }
        else if (WaveBrush is { } waveBrush)
        {
            context.FillRectangle(waveBrush, new Rect(0, middle - 0.5, size.Width, 1));
        }

        if (IsOverview && length > 0 && WindowBrush is { } windowBrush)
        {
            var left = Viewport.Start / length * size.Width;
            var width = Math.Max(4, Viewport.Length / length * size.Width);
            context.DrawRectangle(windowBrush, new Pen(PositionBrush, 1), new Rect(left, 0.5, width, size.Height - 1));
        }

        if (length > 0 && positionX >= -1 && positionX <= size.Width + 1 && PositionBrush is { } positionBrush)
        {
            context.FillRectangle(positionBrush, new Rect(positionX - 1, 0, 2, size.Height));
        }
    }

    /// <summary>Die Form der Wellenform als eine Fläche: oben die größten Werte, unten die kleinsten.</summary>
    private StreamGeometry? Shape(Size size, double start, double length)
    {
        if (Peaks is not { } peaks || length <= 0)
        {
            return null;
        }

        var key = (peaks, start, length, size);
        if (_shape is not null && _shapeKey == key)
        {
            return _shape;
        }

        var columns = Math.Max(1, (int)Math.Ceiling(size.Width));
        if (_min.Length != columns)
        {
            _min = new float[columns];
            _max = new float[columns];
        }

        var framesPerColumn = length * peaks.SampleRate / columns;
        peaks.Envelope(start * peaks.SampleRate, framesPerColumn, _min, _max);

        var middle = size.Height / 2;
        var scale = middle * 0.95;
        var step = size.Width / columns;
        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            context.BeginFigure(new Point(0, middle - Top(0)), isFilled: true);
            for (var i = 0; i < columns; i++)
            {
                context.LineTo(new Point((i + 0.5) * step, middle - Top(i)));
            }

            context.LineTo(new Point(size.Width, middle - Top(columns - 1)));
            context.LineTo(new Point(size.Width, middle - Bottom(columns - 1)));
            for (var i = columns - 1; i >= 0; i--)
            {
                context.LineTo(new Point((i + 0.5) * step, middle - Bottom(i)));
            }

            context.LineTo(new Point(0, middle - Bottom(0)));
            context.EndFigure(isClosed: true);
        }

        _shape = geometry;
        _shapeKey = key;
        return geometry;

        // Mindestens ein Pixel hoch, damit auch Stille als Linie zu sehen ist.
        double Top(int i) => Math.Max(_max[i] * scale, (_min[i] * scale) + 1);
        double Bottom(int i) => _min[i] * scale;
    }

    private double TimeAt(double x)
    {
        var (start, length) = Shown;
        return start + (Math.Clamp(x / Math.Max(1, Bounds.Width), 0, 1) * length);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (Viewport.Duration <= 0 || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        var point = e.GetPosition(this);
        _pointers[e.Pointer] = point;
        e.Pointer.Capture(this);
        e.Handled = true;

        if (IsOverview)
        {
            Viewport = Viewport.CenterOn(TimeAt(point.X));
            return;
        }

        if (_pointers.Count == 1)
        {
            _pressPoint = point;
            _gestureViewport = Viewport;
            _dragging = false;
            _pinching = false;
            _gestureUsed = false;
        }
        else if (_pointers.Count == 2)
        {
            StartPinch();
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (!_pointers.ContainsKey(e.Pointer))
        {
            return;
        }

        var point = e.GetPosition(this);
        _pointers[e.Pointer] = point;
        e.Handled = true;

        if (IsOverview)
        {
            Viewport = Viewport.CenterOn(TimeAt(point.X));
            return;
        }

        if (_pinching && _pointers.Count >= 2)
        {
            var (first, second) = TwoPointers();
            var distance = Math.Max(1, Math.Abs(second.X - first.X));
            var length = _gestureViewport.Length * _pinchDistance / distance;
            var fraction = (first.X + second.X) / 2 / Math.Max(1, Bounds.Width);
            Viewport = _gestureViewport.Place(_pinchAnchor, fraction, length);
            return;
        }

        if (!_dragging && Math.Abs(point.X - _pressPoint.X) + Math.Abs(point.Y - _pressPoint.Y) > DragThreshold)
        {
            _dragging = true;
            _gestureUsed = true;
        }

        if (_dragging && Bounds.Width > 0)
        {
            var seconds = (point.X - _pressPoint.X) / Bounds.Width * _gestureViewport.Length;
            Viewport = _gestureViewport.WithStart(_gestureViewport.Start - seconds);
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (!_pointers.Remove(e.Pointer, out var point))
        {
            return;
        }

        e.Pointer.Capture(null);
        e.Handled = true;
        if (IsOverview)
        {
            return;
        }

        if (_pointers.Count == 0)
        {
            if (!_gestureUsed)
            {
                Position = TimeAt(point.X);
            }

            _pinching = false;
            _dragging = false;
        }
        else if (_pinching)
        {
            // Ein Finger bleibt liegen: Er verschiebt ab hier, ein Tipp ist es nicht mehr.
            _pinching = false;
            _dragging = true;
            _pressPoint = _pointers.Values.First();
            _gestureViewport = Viewport;
        }
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        _pointers.Remove(e.Pointer);
        if (_pointers.Count == 0)
        {
            _pinching = false;
            _dragging = false;
        }
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        if (Viewport.Duration <= 0 || IsOverview)
        {
            return;
        }

        e.Handled = true;
        if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            Viewport = Viewport.ZoomAround(Math.Pow(WheelZoom, e.Delta.Y), TimeAt(e.GetPosition(this).X));
            return;
        }

        var steps = -e.Delta.Y - e.Delta.X;
        Viewport = Viewport.WithStart(Viewport.Start + (steps * WheelPan * Viewport.Length));
    }

    private void StartPinch()
    {
        var (first, second) = TwoPointers();
        _pinching = true;
        _dragging = false;
        _gestureUsed = true;
        _gestureViewport = Viewport;
        _pinchDistance = Math.Max(1, Math.Abs(second.X - first.X));
        _pinchAnchor = TimeAt((first.X + second.X) / 2);
    }

    private (Point First, Point Second) TwoPointers()
    {
        using var points = _pointers.Values.GetEnumerator();
        points.MoveNext();
        var first = points.Current;
        points.MoveNext();
        return (first, points.Current);
    }
}
