using CommunityToolkit.Mvvm.ComponentModel;
using JustPractise.Core.Settings;
using JustPractise.Storage;

namespace JustPractise.App.Shell;

/// <summary>
/// Der Entwickler-Modus: schaltet Angaben für die Fehlersuche ein, z. B. die verspäteten Audio-Callbacks in der
/// Statusleiste. Standardmäßig aus und pro Rechner im gerätespezifischen Gedächtnis gespeichert.
/// </summary>
public sealed partial class DeveloperMode(DataFileKeeper<DeviceMemory> memory) : ObservableObject
{
    public bool IsEnabled
    {
        get => memory.Value.Developer.Enabled;
        set
        {
            if (value == IsEnabled)
            {
                return;
            }

            memory.Update(current => current with { Developer = current.Developer with { Enabled = value } });
            OnPropertyChanged();
        }
    }
}
