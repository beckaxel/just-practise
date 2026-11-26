using System.ComponentModel;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;

namespace JustPractise.App.Timer;

public sealed partial class TimerToolView : UserControl
{
    /// <summary>Öffnet die Einstellungen des Timers; kommt vom Hauptfenster.</summary>
    public static readonly StyledProperty<ICommand?> OpenCommandProperty =
        AvaloniaProperty.Register<TimerToolView, ICommand?>(nameof(OpenCommand));

    private TimerViewModel? _viewModel;

    public TimerToolView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (_viewModel is not null)
            {
                _viewModel.PropertyChanged -= OnViewModelChanged;
            }

            _viewModel = DataContext as TimerViewModel;
            if (_viewModel is not null)
            {
                _viewModel.PropertyChanged += OnViewModelChanged;
            }

            UpdateFill();
        };
        Track.SizeChanged += (_, _) => UpdateFill();
    }

    /// <summary>Der Knopf Start/Pause; das Hauptfenster prüft, ob ein Tipp neben den Einstellungen ihn trifft.</summary>
    public Control StartPause => StartPauseButton;

    public ICommand? OpenCommand
    {
        get => GetValue(OpenCommandProperty);
        set => SetValue(OpenCommandProperty, value);
    }

    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TimerViewModel.Fraction))
        {
            UpdateFill();
        }
    }

    /// <summary>Der gefüllte Teil zeigt, wie viel noch übrig ist.</summary>
    private void UpdateFill() => Fill.Width = Track.Bounds.Width * Math.Clamp(_viewModel?.Fraction ?? 1, 0, 1);
}
