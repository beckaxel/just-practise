using Tmds.DBus.Protocol;

namespace JustPractise.App.Shell.ScreenSaver;

/// <summary>
/// Unter Linux über die D-Bus-Schnittstelle <c>org.freedesktop.ScreenSaver</c>, die KDE, GNOME, Xfce und andere
/// anbieten. Die Sperre hängt an der Verbindung: Endet die App, gibt der Desktop sie von selbst frei.
/// Fehlt die Schnittstelle, läuft der Bildschirmschoner wie gewohnt.
/// </summary>
/// <remarks>
/// Mit eigener Verbindung, die bei <see cref="Dispose"/> endet und damit die Sperre sicher freigibt. Geht sie
/// verloren, wird sie neu aufgebaut.
/// </remarks>
public sealed class FreedesktopScreenSaverInhibitor : IScreenSaverInhibitor, IDisposable
{
    private const string Service = "org.freedesktop.ScreenSaver";
    private const string ObjectPath = "/org/freedesktop/ScreenSaver";

    private readonly Lock _lock = new();
    private Task _pending = Task.CompletedTask;
    private DBusConnection? _connection;
    private uint? _cookie;

    public void Inhibit(bool inhibit)
    {
        // Nacheinander, damit ein schnelles Ein und Aus in der richtigen Reihenfolge ankommt.
        lock (_lock)
        {
            _pending = _pending.ContinueWith(_ => ApplyAsync(inhibit), TaskScheduler.Default).Unwrap();
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            _connection?.Dispose();
            _connection = null;
        }
    }

    private async Task ApplyAsync(bool inhibit)
    {
        try
        {
            if (inhibit && _cookie is null)
            {
                _cookie = await CallAsync(inhibit: true, cookie: 0).ConfigureAwait(false);
            }
            else if (!inhibit && _cookie is { } cookie)
            {
                _cookie = null;
                await CallAsync(inhibit: false, cookie).ConfigureAwait(false);
            }
        }
        catch (Exception exception) when (exception is DBusExceptionBase or InvalidOperationException or ObjectDisposedException)
        {
            // Kein Bildschirmschoner-Dienst oder keine Sitzung: nichts zu tun.
        }
    }

    /// <summary>Ruft <c>Inhibit</c> bzw. <c>UnInhibit</c> auf; ist die Verbindung verloren, einmal mit einer neuen.</summary>
    private async Task<uint> CallAsync(bool inhibit, uint cookie)
    {
        try
        {
            return await CallOnceAsync(inhibit, cookie).ConfigureAwait(false);
        }
        catch (DBusConnectionException)
        {
            Reset();
            return await CallOnceAsync(inhibit, cookie).ConfigureAwait(false);
        }
    }

    private async Task<uint> CallOnceAsync(bool inhibit, uint cookie)
    {
        var connection = await ConnectAsync().ConfigureAwait(false);
        MessageBuffer message;

        // MessageWriter ist ein Struct: nie als Wert weitergeben, sonst landen die Argumente in einer Kopie und der
        // Bus trennt die Verbindung wegen der fehlerhaften Nachricht.
        using (var writer = connection.GetMessageWriter())
        {
            if (inhibit)
            {
                writer.WriteMethodCallHeader(Service, ObjectPath, Service, "Inhibit", "ss", MessageFlags.None);
                writer.WriteString("just practise");
                writer.WriteString("Practising");
            }
            else
            {
                writer.WriteMethodCallHeader(Service, ObjectPath, Service, "UnInhibit", "u", MessageFlags.None);
                writer.WriteUInt32(cookie);
            }

            message = writer.CreateMessage();
        }

        return inhibit
            ? await connection.CallMethodAsync(message, static (reply, _) => reply.GetBodyReader().ReadUInt32(), null).ConfigureAwait(false)
            : await connection.CallMethodAsync(message, static (_, _) => 0u, null).ConfigureAwait(false);
    }

    private async Task<DBusConnection> ConnectAsync()
    {
        DBusConnection connection;
        lock (_lock)
        {
            if (_connection is not null)
            {
                return _connection;
            }

            var address = DBusAddress.Session ?? throw new InvalidOperationException("No D-Bus session.");
            _connection = connection = new DBusConnection(address);
        }

        await connection.ConnectAsync().ConfigureAwait(false);
        return connection;
    }

    private void Reset()
    {
        lock (_lock)
        {
            _connection?.Dispose();
            _connection = null;
        }
    }
}
