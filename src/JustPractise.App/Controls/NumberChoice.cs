using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace JustPractise.App.Controls;

/// <summary>Eine Voreinstellung als Knopf (z. B. 90 BPM oder 15 Minuten); <see cref="IsSelected"/> markiert die aktuelle.</summary>
public sealed partial class NumberChoice(int value) : ObservableObject
{
    public int Value { get; } = value;

    public string Label => Value.ToString(CultureInfo.InvariantCulture);

    [ObservableProperty]
    public partial bool IsSelected { get; set; }
}
