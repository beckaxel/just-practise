namespace JustPractise.Prototypes.GcCheck;

/// <summary>
/// Legt auf dem UI-Thread Speicher an, wie es eine geschäftige Oberfläche tut, und hält auf Wunsch
/// ein großes Objektgeflecht dauerhaft am Leben. Das Markieren eines vollen GC dauert umso länger,
/// je mehr lebende Objekte es gibt, nicht je mehr Bytes.
/// </summary>
internal sealed class LoadGenerator
{
    private const int SurvivorSlots = 50_000;
    private const int LargeSlots = 64;
    private const int LargeObjectBytes = 1 << 20;

    private readonly Random _random = new(1);
    private readonly object?[] _survivors = new object?[SurvivorSlots];
    private readonly byte[]?[] _large = new byte[]?[LargeSlots];
    private Node[] _retained = [];
    private double _pendingBytes;
    private int _largeIndex;

    /// <summary>Kurzlebige Objekte in MB pro Sekunde.</summary>
    public double MegabytesPerSecond { get; set; }

    /// <summary>Anteil der Objekte, die eine Weile überleben und so in Generation 1 und 2 gelangen.</summary>
    public double SurvivalPercent { get; set; } = 2;

    /// <summary>Legt pro Frame ein Objekt von 1 MB auf dem Large Object Heap an.</summary>
    public bool LargeObjects { get; set; }

    public int RetainedObjects => _retained.Length;

    /// <summary>Baut ein Geflecht aus <paramref name="count"/> verknüpften Objekten auf, das dauerhaft lebt.</summary>
    public void SetRetainedObjects(int count)
    {
        _retained = [];
        GC.Collect();
        var nodes = new Node[count];
        for (var i = 0; i < count; i++)
        {
            nodes[i] = new Node(i, i == 0 ? null : nodes[_random.Next(i)]);
        }

        _retained = nodes;
    }

    public void Tick(TimeSpan elapsed)
    {
        _pendingBytes += MegabytesPerSecond * 1_000_000 * Math.Min(elapsed.TotalSeconds, 0.25);
        while (_pendingBytes > 0)
        {
            var size = _random.Next(16, 2048);
            var array = new byte[size];
            _pendingBytes -= size + 24;
            if (_random.NextDouble() * 100 < SurvivalPercent)
            {
                // Ersetzt einen zufälligen Platz: Alte Objekte zeigen so auf junge, wie in echten Anwendungen.
                _survivors[_random.Next(SurvivorSlots)] = array;
            }
        }

        if (LargeObjects)
        {
            _large[_largeIndex] = new byte[LargeObjectBytes];
            _largeIndex = (_largeIndex + 1) % LargeSlots;
        }
    }

    private sealed class Node(int value, Node? next)
    {
        public int Value { get; } = value;

        public Node? Next { get; } = next;
    }
}
