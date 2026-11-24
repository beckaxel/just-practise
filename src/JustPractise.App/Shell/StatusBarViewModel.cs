using CommunityToolkit.Mvvm.ComponentModel;
using JustPractise.App.Localization;
using JustPractise.Audio;

namespace JustPractise.App.Shell;

/// <summary>
/// Die Statusleiste: das Ausgabegerät (oder dass es fehlt) und, nur im Entwickler-Modus und nur wenn es welche gab,
/// die Zahl der verspäteten Audio-Callbacks (Prüfpunkt 1).
/// </summary>
public sealed partial class StatusBarViewModel(IAudioDevice device, Translator translator, DeveloperMode developer) : ObservableObject
{
    private (bool Available, string? Name, long Late, string Language)? _shown;

    [ObservableProperty]
    public partial bool HasDevice { get; private set; } = true;

    [ObservableProperty]
    public partial string DeviceText { get; private set; } = "";

    [ObservableProperty]
    public partial bool HasLateCallbacks { get; private set; }

    [ObservableProperty]
    public partial string LateText { get; private set; } = "";

    /// <summary>Holt den Stand des Geräts; die Oberfläche ruft das etwa 30-mal pro Sekunde auf.</summary>
    public void Tick()
    {
        var available = device.IsAvailable;
        var current = (available, device.Name, available && developer.IsEnabled ? device.LateCallbacks : 0, translator.Language);
        if (current == _shown)
        {
            return;
        }

        _shown = current;
        HasDevice = available;
        DeviceText = !available
            ? translator.Get("status.noDevice")
            : current.Name is { } name
                ? translator.Format("status.device", new Dictionary<string, object?> { ["name"] = name })
                : translator.Get("status.defaultDevice");
        HasLateCallbacks = current.Item3 > 0;
        LateText = HasLateCallbacks
            ? translator.Format("status.late", new Dictionary<string, object?> { ["count"] = current.Item3 })
            : "";
    }
}
