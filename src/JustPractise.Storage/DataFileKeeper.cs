namespace JustPractise.Storage;

/// <summary>
/// Hält den aktuellen Inhalt einer Datendatei im Speicher und schreibt Änderungen im Hintergrund, eine nach der
/// anderen. Die Oberfläche wartet also nie auf die Platte. Kommen Änderungen schneller, als geschrieben werden kann
/// (z. B. beim Ziehen eines Reglers), wird nur der jeweils neueste Stand geschrieben.
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
    private T? _unsaved;

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
            if (_unsaved is not null)
            {
                _unsaved = value; // Ein Schreibvorgang wartet schon und nimmt dann diesen Stand.
                return;
            }

            _unsaved = value;
            _saving = _saving.ContinueWith(_ => SaveUnsaved(), CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.Default);
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

    private void SaveUnsaved()
    {
        T value;
        lock (_lock)
        {
            value = _unsaved!;
            _unsaved = null;
        }

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
