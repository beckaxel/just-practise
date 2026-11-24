using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace JustPractise.App.Controls;

/// <summary>
/// Ein einstellbarer Wert: [−] Regler [+] und daneben der Wert. Gleich in Player und Metronom, damit sich Tempo und
/// Lautstärke überall gleich bedienen lassen, auch mit dem Finger: Die Knöpfe ändern um <see cref="Step"/> und
/// wiederholen, solange man sie hält; der Regler ist für große Sprünge.
/// </summary>
public sealed partial class StepSlider : UserControl
{
    public static readonly StyledProperty<double> ValueProperty =
        AvaloniaProperty.Register<StepSlider, double>(nameof(Value), defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<double> MinimumProperty = AvaloniaProperty.Register<StepSlider, double>(nameof(Minimum));

    public static readonly StyledProperty<double> MaximumProperty = AvaloniaProperty.Register<StepSlider, double>(nameof(Maximum), 100);

    public static readonly StyledProperty<double> StepProperty = AvaloniaProperty.Register<StepSlider, double>(nameof(Step), 1);

    public static readonly StyledProperty<double> LargeChangeProperty = AvaloniaProperty.Register<StepSlider, double>(nameof(LargeChange), 10);

    public static readonly StyledProperty<Geometry?> IconProperty = AvaloniaProperty.Register<StepSlider, Geometry?>(nameof(Icon));

    public static readonly StyledProperty<string?> LabelProperty = AvaloniaProperty.Register<StepSlider, string?>(nameof(Label));

    public static readonly StyledProperty<string?> ValueTextProperty = AvaloniaProperty.Register<StepSlider, string?>(nameof(ValueText));

    public static readonly StyledProperty<double> ValueWidthProperty = AvaloniaProperty.Register<StepSlider, double>(nameof(ValueWidth), 76);

    public static readonly StyledProperty<ICommand?> ValueCommandProperty = AvaloniaProperty.Register<StepSlider, ICommand?>(nameof(ValueCommand));

    public static readonly StyledProperty<string?> ValueTipProperty = AvaloniaProperty.Register<StepSlider, string?>(nameof(ValueTip));

    public static readonly StyledProperty<FlyoutBase?> ValueFlyoutProperty = AvaloniaProperty.Register<StepSlider, FlyoutBase?>(nameof(ValueFlyout));

    public static readonly StyledProperty<string?> DecreaseTipProperty = AvaloniaProperty.Register<StepSlider, string?>(nameof(DecreaseTip));

    public static readonly StyledProperty<string?> IncreaseTipProperty = AvaloniaProperty.Register<StepSlider, string?>(nameof(IncreaseTip));

    private bool _syncing;

    public StepSlider()
    {
        InitializeComponent();
        Sync();
        Slider.ValueChanged += (_, e) =>
        {
            if (!_syncing)
            {
                Value = e.NewValue;
            }
        };
        ValueButton.Click += (_, _) =>
        {
            if (ValueFlyout is { } flyout)
            {
                flyout.ShowAt(ValueButton);
            }
        };
    }

    public double Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public double Minimum
    {
        get => GetValue(MinimumProperty);
        set => SetValue(MinimumProperty, value);
    }

    public double Maximum
    {
        get => GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    /// <summary>Schrittweite der Knöpfe und Raster des Reglers.</summary>
    public double Step
    {
        get => GetValue(StepProperty);
        set => SetValue(StepProperty, value);
    }

    public double LargeChange
    {
        get => GetValue(LargeChangeProperty);
        set => SetValue(LargeChangeProperty, value);
    }

    /// <summary>Symbol vor dem Regler; spart gegenüber einer Beschriftung Platz.</summary>
    public Geometry? Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    /// <summary>Name des Werts, als Tooltip des Symbols.</summary>
    public string? Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    /// <summary>Der angezeigte Wert, z. B. „100 %“.</summary>
    public string? ValueText
    {
        get => GetValue(ValueTextProperty);
        set => SetValue(ValueTextProperty, value);
    }

    public double ValueWidth
    {
        get => GetValue(ValueWidthProperty);
        set => SetValue(ValueWidthProperty, value);
    }

    /// <summary>Ein Tipp auf den Wert, z. B. zurücksetzen.</summary>
    public ICommand? ValueCommand
    {
        get => GetValue(ValueCommandProperty);
        set => SetValue(ValueCommandProperty, value);
    }

    public string? ValueTip
    {
        get => GetValue(ValueTipProperty);
        set => SetValue(ValueTipProperty, value);
    }

    /// <summary>Öffnet sich bei einem Tipp auf den Wert, z. B. zur direkten Eingabe.</summary>
    public FlyoutBase? ValueFlyout
    {
        get => GetValue(ValueFlyoutProperty);
        set => SetValue(ValueFlyoutProperty, value);
    }

    public string? DecreaseTip
    {
        get => GetValue(DecreaseTipProperty);
        set => SetValue(DecreaseTipProperty, value);
    }

    public string? IncreaseTip
    {
        get => GetValue(IncreaseTipProperty);
        set => SetValue(IncreaseTipProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        ArgumentNullException.ThrowIfNull(change);
        base.OnPropertyChanged(change);
        if (change.Property == ValueProperty || change.Property == MinimumProperty || change.Property == MaximumProperty)
        {
            Sync();
        }
    }

    /// <summary>
    /// Überträgt Grenzen und Wert auf den Regler: erst die Grenzen, dann den Wert. Mit Bindungen könnte der Regler
    /// den Wert an noch nicht gesetzte Grenzen anpassen (z. B. 120 BPM auf den Standard-Höchstwert 100) und das über
    /// die Bindung ins ViewModel zurückschreiben. Was der Regler beim Abgleich anpasst, wird deshalb ignoriert.
    /// </summary>
    private void Sync()
    {
        if (Slider is null)
        {
            return;
        }

        _syncing = true;
        try
        {
            Slider.Minimum = Math.Min(Minimum, Maximum);
            Slider.Maximum = Math.Max(Minimum, Maximum);
            Slider.Value = Value;
        }
        finally
        {
            _syncing = false;
        }
    }

    /// <summary>Ändert den Wert um <paramref name="steps"/> Schritte, im Raster und innerhalb der Grenzen.</summary>
    public static double StepFrom(double value, int steps, double step, double minimum, double maximum)
    {
        var snapped = Math.Round(value / step) * step;
        return Math.Clamp(Math.Round((snapped + (steps * step)) / step) * step, minimum, maximum);
    }

    private void OnDecrease(object? sender, RoutedEventArgs e) => Value = StepFrom(Value, -1, Step, Minimum, Maximum);

    private void OnIncrease(object? sender, RoutedEventArgs e) => Value = StepFrom(Value, 1, Step, Minimum, Maximum);
}
