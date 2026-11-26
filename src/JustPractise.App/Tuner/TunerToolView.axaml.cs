using System.ComponentModel;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;

namespace JustPractise.App.Tuner;

public sealed partial class TunerToolView : UserControl
{
    /// <summary>Öffnet die Einstellungen des Stimmgeräts; kommt vom Hauptfenster.</summary>
    public static readonly StyledProperty<ICommand?> OpenCommandProperty =
        AvaloniaProperty.Register<TunerToolView, ICommand?>(nameof(OpenCommand));

    private TunerViewModel? _viewModel;

    public TunerToolView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (_viewModel is not null)
            {
                _viewModel.PropertyChanged -= OnViewModelChanged;
            }

            _viewModel = DataContext as TunerViewModel;
            if (_viewModel is not null)
            {
                _viewModel.PropertyChanged += OnViewModelChanged;
            }

            UpdateFill();
        };
        Track.SizeChanged += (_, _) => UpdateFill();
    }

    /// <summary>Der Knopf An/Aus; das Hauptfenster prüft, ob ein Tipp neben den Einstellungen ihn trifft.</summary>
    public Control Toggle => ToggleButton;

    public ICommand? OpenCommand
    {
        get => GetValue(OpenCommandProperty);
        set => SetValue(OpenCommandProperty, value);
    }

    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(TunerViewModel.Deviation) or nameof(TunerViewModel.Level))
        {
            UpdateFill();
        }
    }

    /// <summary>Von der Mitte aus: nach links zu tief, nach rechts zu hoch; 50 Cent füllen eine Hälfte.</summary>
    private void UpdateFill()
    {
        var half = Track.Bounds.Width / 2;
        var deviation = _viewModel?.Deviation ?? 0;
        Flat.Width = deviation < 0 ? half * -deviation : 0;
        Sharp.Width = deviation > 0 ? half * deviation : 0;
        LevelFill.Width = Track.Bounds.Width * Math.Clamp(_viewModel?.Level ?? 0, 0, 1);
    }
}
