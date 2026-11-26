using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;

namespace JustPractise.App.Metronome;

public sealed partial class MetronomeToolView : UserControl
{
    /// <summary>Öffnet die Einstellungen des Metronoms; kommt vom Hauptfenster.</summary>
    public static readonly StyledProperty<ICommand?> OpenCommandProperty =
        AvaloniaProperty.Register<MetronomeToolView, ICommand?>(nameof(OpenCommand));

    public MetronomeToolView() => InitializeComponent();

    /// <summary>Der Knopf Start/Stopp; das Hauptfenster prüft, ob ein Tipp neben den Einstellungen ihn trifft.</summary>
    public Control StartStop => StartStopButton;

    public ICommand? OpenCommand
    {
        get => GetValue(OpenCommandProperty);
        set => SetValue(OpenCommandProperty, value);
    }
}
