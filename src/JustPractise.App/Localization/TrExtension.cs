using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;

namespace JustPractise.App.Localization;

/// <summary>
/// Übersetzt in XAML: <c>{l:Tr player.play}</c>. Die Bindung hängt an <see cref="Translator.Language"/>, sodass sich
/// der Text beim Wechsel der Sprache ohne Neustart aktualisiert.
/// </summary>
/// <remarks>
/// XAML erzeugt Markup-Erweiterungen selbst, ohne Konstruktor-Injektion. Deshalb setzt der Start der App den
/// Übersetzer einmal in <see cref="Current"/>; das ist die einzige solche Stelle. Ohne ihn (z. B. im Designer)
/// erscheint der Schlüssel.
/// </remarks>
public sealed class TrExtension(string key)
{
    public static Translator? Current { get; set; }

    public object ProvideValue(IServiceProvider serviceProvider)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);
        return Current is { } translator
            ? new ReflectionBinding(nameof(Translator.Language)) { Source = translator, Mode = BindingMode.OneWay, Converter = new KeyConverter(translator, key) }
            : key;
    }

    /// <summary>Wandelt jede Änderung der Sprache in den Text zum Schlüssel.</summary>
    private sealed class KeyConverter(Translator translator, string key) : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => translator.Get(key);

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }
}
