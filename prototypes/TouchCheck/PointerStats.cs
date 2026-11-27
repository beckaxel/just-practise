using System.Globalization;
using System.Text;
using Avalonia.Input;

namespace JustPractise.Prototypes.TouchCheck;

/// <summary>Zählt, was an Zeiger-Ereignissen und Gesten ankommt, für den Bericht.</summary>
internal sealed class PointerStats
{
    private readonly Dictionary<PointerType, int> _presses = [];
    private readonly Dictionary<PointerType, int> _moves = [];
    private readonly Dictionary<string, int> _gestures = new(StringComparer.Ordinal);
    private readonly HashSet<long> _activeTouches = [];

    public int MaxSimultaneousTouches { get; private set; }

    public float MinPressure { get; private set; } = float.MaxValue;

    public float MaxPressure { get; private set; } = float.MinValue;

    public int DistinctPressures => _pressures.Count;

    public bool SawTilt { get; private set; }

    public bool SawEraser { get; private set; }

    public bool SawBarrelButton { get; private set; }

    /// <summary>Höchste Rate an Bewegungs-Ereignissen pro Sekunde (einschließlich Zwischenpunkte), je Gerät.</summary>
    public Dictionary<PointerType, double> MaxMoveRate { get; } = [];

    private readonly HashSet<int> _pressures = [];

    public int ActiveTouches => _activeTouches.Count;

    public void Pressed(PointerType type, long id)
    {
        _presses[type] = _presses.GetValueOrDefault(type) + 1;
        if (type == PointerType.Touch)
        {
            _activeTouches.Add(id);
            MaxSimultaneousTouches = Math.Max(MaxSimultaneousTouches, _activeTouches.Count);
        }
    }

    public void Released(PointerType type, long id)
    {
        if (type == PointerType.Touch)
        {
            _activeTouches.Remove(id);
        }
    }

    public void Moved(PointerType type, PointerPointProperties properties, int points)
    {
        _moves[type] = _moves.GetValueOrDefault(type) + points;
        if (type != PointerType.Mouse)
        {
            MinPressure = Math.Min(MinPressure, properties.Pressure);
            MaxPressure = Math.Max(MaxPressure, properties.Pressure);
            _pressures.Add((int)Math.Round(properties.Pressure * 1000));
        }

        SawTilt |= properties.XTilt != 0 || properties.YTilt != 0;
        SawEraser |= properties.IsEraser || properties.IsInverted;
        SawBarrelButton |= properties.IsBarrelButtonPressed;
    }

    public void MoveRate(PointerType type, double perSecond) =>
        MaxMoveRate[type] = Math.Max(MaxMoveRate.GetValueOrDefault(type), perSecond);

    public void Gesture(string name) => _gestures[name] = _gestures.GetValueOrDefault(name) + 1;

    public string Report(string environment)
    {
        var culture = CultureInfo.InvariantCulture;
        var text = new StringBuilder();
        text.AppendLine(environment);
        text.AppendLine();
        foreach (var type in new[] { PointerType.Mouse, PointerType.Touch, PointerType.Pen })
        {
            text.AppendLine(culture, $"{type,-6} gedrückt {_presses.GetValueOrDefault(type),5}, Bewegungspunkte {_moves.GetValueOrDefault(type),7}, höchste Rate {MaxMoveRate.GetValueOrDefault(type),5:0}/s");
        }

        text.AppendLine(culture, $"gleichzeitige Finger höchstens: {MaxSimultaneousTouches}");
        text.AppendLine(_pressures.Count == 0
            ? "Druck: keine Werte von Touch oder Stift"
            : string.Create(culture, $"Druck: {MinPressure:0.000} bis {MaxPressure:0.000}, {DistinctPressures} verschiedene Werte"));
        text.AppendLine(culture, $"Neigung gesehen: {SawTilt}, Radierer: {SawEraser}, Stifttaste: {SawBarrelButton}");
        text.AppendLine("Gesten und Bedienelemente:");
        foreach (var (name, count) in _gestures.OrderBy(entry => entry.Key, StringComparer.Ordinal))
        {
            text.AppendLine(culture, $"  {name}: {count}");
        }

        return text.ToString();
    }
}
