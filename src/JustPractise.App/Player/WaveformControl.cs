using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Media;
using JustPractise.Core.Waveform;

namespace JustPractise.App.Player;

/// <summary>
/// Zeichnet die Wellenform des ganzen Stücks mit der Abspielposition. Tippen oder Ziehen mit Maus oder Finger
/// springt an die Stelle, wie beim Fortschrittsbalken.
/// </summary>
/// <remarks>
/// Die Form der Wellenform wird nur neu berechnet, wenn sich Länge, Größe oder Spitzen ändern; die Abspielposition
/// ändert nur, wo die Farbe wechselt.
/// </remarks>
public sealed class WaveformControl : Control
{
    public static readonly StyledProperty<WaveformPeaks?> PeaksProperty =
        AvaloniaProperty.Register<WaveformControl, WaveformPeaks?>(nameof(Peaks));

    public static readonly StyledProperty<double> DurationProperty =
        AvaloniaProperty.Register<WaveformControl, double>(nameof(Duration));

    public static readonly StyledProperty<double> PositionProperty =
        AvaloniaProperty.Register<WaveformControl, double>(nameof(Position), defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<IBrush?> BackgroundProperty =
        AvaloniaProperty.Register<WaveformControl, IBrush?>(nameof(Background));

    public static readonly StyledProperty<IBrush?> WaveBrushProperty =
        AvaloniaProperty.Register<WaveformControl, IBrush?>(nameof(WaveBrush), Brushes.Gray);

    public static readonly StyledProperty<IBrush?> PlayedBrushProperty =
        AvaloniaProperty.Register<WaveformControl, IBrush?>(nameof(PlayedBrush), Brushes.SteelBlue);

    public static readonly StyledProperty<IBrush?> PositionBrushProperty =
        AvaloniaProperty.Register<WaveformControl, IBrush?>(nameof(PositionBrush), Brushes.White);

    private float[] _min = [];
    private float[] _max = [];
    private StreamGeometry? _shape;
    private (WaveformPeaks? Peaks, double Duration, Size Size) _shapeKey;
    private IPointer? _pointer;

    static WaveformControl()
    {
        AffectsRender<WaveformControl>(
            PeaksProperty, DurationProperty, PositionProperty, BackgroundProperty, WaveBrushProperty, PlayedBrushProperty,
            PositionBrushProperty);
        ClipToBoundsProperty.OverrideDefaultValue<WaveformControl>(true);
    }

    public WaveformPeaks? Peaks
    {
        get => GetValue(PeaksProperty);
        set => SetValue(PeaksProperty, value);
    }

    /// <summary>Die Länge des Stücks in Sekunden, soweit schon decodiert.</summary>
    public double Duration
    {
        get => GetValue(DurationProperty);
        set => SetValue(DurationProperty, value);
    }

    /// <summary>Die Abspielposition in Sekunden. Tippen oder Ziehen setzt sie.</summary>
    public double Position
    {
        get => GetValue(PositionProperty);
        set => SetValue(PositionProperty, value);
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

    public override void Render(DrawingContext context)
    {
        var size = Bounds.Size;

        // Auch ohne Hintergrund überall treffbar, damit Tippen neben der Kurve wirkt.
        context.FillRectangle(Background ?? Brushes.Transparent, new Rect(size));
        if (size.Width <= 0 || size.Height <= 0)
        {
            return;
        }

        var positionX = Duration > 0 ? Math.Clamp(Position / Duration, 0, 1) * size.Width : 0;
        if (Shape(size) is { } shape)
        {
            using (context.PushClip(new Rect(0, 0, positionX, size.Height)))
            {
                context.DrawGeometry(PlayedBrush, null, shape);
            }

            using (context.PushClip(new Rect(positionX, 0, size.Width - positionX, size.Height)))
            {
                context.DrawGeometry(WaveBrush, null, shape);
            }
        }
        else if (WaveBrush is { } waveBrush)
        {
            context.FillRectangle(waveBrush, new Rect(0, (size.Height / 2) - 0.5, size.Width, 1));
        }

        if (Duration > 0 && PositionBrush is { } positionBrush)
        {
            context.FillRectangle(positionBrush, new Rect(positionX - 1, 0, 2, size.Height));
        }
    }

    /// <summary>Die Form der Wellenform als eine Fläche: oben die größten Werte, unten die kleinsten.</summary>
    private StreamGeometry? Shape(Size size)
    {
        if (Peaks is not { } peaks || Duration <= 0)
        {
            return null;
        }

        var key = (peaks, Duration, size);
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

        peaks.Envelope(0, Duration * peaks.SampleRate / columns, _min, _max);

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

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (_pointer is not null || Duration <= 0 || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        _pointer = e.Pointer;
        e.Pointer.Capture(this);
        e.Handled = true;
        SeekTo(e.GetPosition(this).X);
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (e.Pointer == _pointer)
        {
            e.Handled = true;
            SeekTo(e.GetPosition(this).X);
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (e.Pointer == _pointer)
        {
            e.Handled = true;
            e.Pointer.Capture(null);
            _pointer = null;
        }
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        if (e.Pointer == _pointer)
        {
            _pointer = null;
        }
    }

    private void SeekTo(double x) => Position = Math.Clamp(x / Math.Max(1, Bounds.Width), 0, 1) * Duration;
}
