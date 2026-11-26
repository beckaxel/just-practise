using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JustPractise.Core.Settings;
using JustPractise.Storage;

namespace JustPractise.App.Shell;

/// <summary>
/// Breite der Bibliothek neben dem Übungsbereich: per Trenner verstellbar und mit einem Knopf auf eine schmale Leiste
/// einklappbar; ausgeklappt hat sie wieder die letzte Breite. Beides merkt sich das gerätespezifische Gedächtnis.
/// </summary>
public sealed partial class LibraryPane : ObservableObject
{
    public const double DefaultWidth = 400;
    public const double MinWidth = 260;
    public const double MaxWidth = 900;

    /// <summary>Breite der eingeklappten Leiste: Platz für einen Knopf in Fingergröße.</summary>
    public const double CollapsedWidth = 56;

    private readonly DataFileKeeper<DeviceMemory> _memory;

    public LibraryPane(DataFileKeeper<DeviceMemory> memory)
    {
        ArgumentNullException.ThrowIfNull(memory);
        _memory = memory;
        var layout = memory.Value.Layout;
        Width = Math.Clamp(layout.LibraryWidth ?? DefaultWidth, MinWidth, MaxWidth);
        IsCollapsed = layout.LibraryCollapsed;
    }

    /// <summary>Die Breite im ausgeklappten Zustand.</summary>
    public double Width { get; private set; }

    [ObservableProperty]
    public partial bool IsCollapsed { get; private set; }

    /// <summary>Nach dem Ziehen am Trenner: die neue Breite merken.</summary>
    public void Resized(double width)
    {
        if (IsCollapsed)
        {
            return;
        }

        Width = Math.Clamp(Math.Round(width), MinWidth, MaxWidth);
        Save();
    }

    /// <summary>Klappt eine eingeklappte Bibliothek aus, z. B. nach einem Tipp in der Pfadzeile.</summary>
    public void Show()
    {
        if (IsCollapsed)
        {
            Toggle();
        }
    }

    [RelayCommand]
    private void Toggle()
    {
        IsCollapsed = !IsCollapsed;
        Save();
    }

    private void Save() => _memory.Update(value =>
    {
        var layout = value.Layout with { LibraryWidth = Width == DefaultWidth ? null : Width, LibraryCollapsed = IsCollapsed };
        return layout == value.Layout ? value : value with { Layout = layout };
    });
}
