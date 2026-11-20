namespace JustPractise.Storage;

/// <summary>
/// Hält den aktuellen Inhalt einer Datendatei im Speicher und schreibt jede Änderung im Hintergrund,
/// eine nach der anderen. Die Oberfläche wartet also nie auf die Platte.
/// </summary>
/// <remarks>
/// Für Einstellungen und Gedächtnis, die nur diese App schreibt. Schlägt das Schreiben fehl
/// (z. B. Platte voll), bleibt der Wert im Speicher gültig; die nächste Änderung versucht es erneut.
/// </remarks>
public sealed class DataFileKeeper<T>
    where T : class, new()
{
    private readonly DataFileStore<T> _store;
    private readonly Lock _lock = new();
    private T _value;
    private Task _saving = Task.CompletedTask;

    /// <summary>Liest die Datei einmal. Blockiert; beim Start der App erzeugen.</summary>
    public DataFileKeeper(DataFileStore<T> store)
    {
        ArgumentNullException.ThrowIfNull(store);
        _store = store;
        _value = store.Load().Value;
    }

    public T Value
    {
        get
        {
            lock (_lock)
            {
                return _value;
            }
        }
    }

    /// <summary>Ändert den Wert sofort und schreibt ihn im Hintergrund. Ohne Änderung wird nichts geschrieben.</summary>
    public void Update(Func<T, T> change)
    {
        ArgumentNullException.ThrowIfNull(change);
        lock (_lock)
        {
            var value = change(_value);
            if (Equals(value, _value))
            {
                return;
            }

            _value = value;
            _saving = _saving.ContinueWith(_ => Save(value), CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.Default);
        }
    }

    /// <summary>Wartet, bis alle bisherigen Änderungen geschrieben sind, z. B. beim Beenden.</summary>
    public Task FlushAsync()
    {
        lock (_lock)
        {
            return _saving;
        }
    }

    private void Save(T value)
    {
        try
        {
            _store.TrySave(value);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Siehe remarks: Der Wert bleibt im Speicher, die nächste Änderung schreibt erneut.
        }
    }
}
