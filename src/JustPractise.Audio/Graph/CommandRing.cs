namespace JustPractise.Audio.Graph;

/// <summary>
/// Lock-freier Ringpuffer, über die Befehle in den Audio-Thread gelangen.
/// </summary>
/// <remarks>
/// Es gibt genau einen Verbraucher, den Audio-Thread. Er holt Befehle ohne Sperre und ohne Allokation ab.
/// Einstellen dürfen mehrere Threads (UI-Thread, Lade-Thread); sie sperren untereinander, nie gegen den Audio-Thread.
/// </remarks>
public sealed class CommandRing<T>
    where T : struct
{
    private readonly T[] _items;
    private readonly int _mask;
    private readonly Lock _producerLock = new();

    // _head wird nur vom Verbraucher geschrieben, _tail nur von Erzeugern (unter _producerLock).
    private long _head;
    private long _tail;

    /// <param name="capacity">Anzahl der Plätze, eine Zweierpotenz</param>
    public CommandRing(int capacity)
    {
        if (capacity < 2 || !int.IsPow2(capacity))
        {
            throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "The capacity must be a power of two.");
        }

        _items = new T[capacity];
        _mask = capacity - 1;
    }

    public int Capacity => _items.Length;

    /// <summary>Stellt einen Befehl ein. Liefert <c>false</c>, wenn die Warteschlange voll ist.</summary>
    public bool TryEnqueue(in T item)
    {
        lock (_producerLock)
        {
            var tail = _tail;
            if (tail - Volatile.Read(ref _head) == _items.Length)
            {
                return false;
            }

            _items[tail & _mask] = item;
            Volatile.Write(ref _tail, tail + 1);
            return true;
        }
    }

    /// <summary>Holt den nächsten Befehl ab. Nur vom Audio-Thread aufrufen.</summary>
    public bool TryDequeue(out T item)
    {
        var head = _head;
        if (head == Volatile.Read(ref _tail))
        {
            item = default;
            return false;
        }

        var index = head & _mask;
        item = _items[index];
        _items[index] = default; // keine Verweise auf alte Puffer festhalten
        Volatile.Write(ref _head, head + 1);
        return true;
    }
}
