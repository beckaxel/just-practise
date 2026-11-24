using Tmds.DBus.Protocol;

namespace JustPractise.App.Shell.ScreenSaver;

/// <summary>
/// Unter Linux über die D-Bus-Schnittstelle <c>org.freedesktop.ScreenSaver</c>, die KDE, GNOME, Xfce und andere
/// anbieten. Die Sperre hängt an der Verbindung: Endet die App, gibt der Desktop sie von selbst frei.
/// Fehlt die Schnittstelle, läuft der Bildschirmschoner wie gewohnt.
/// </summary>
public sealed class FreedesktopScreenSaverInhibitor : IScreenSaverInhibitor
{
    private const string Service = "org.freedesktop.ScreenSaver";
    private const string ObjectPath = "/org/freedesktop/ScreenSaver";

    private readonly Lock _lock = new();
    private Task _pending = Task.CompletedTask;
    private uint? _cookie;

    public void Inhibit(bool inhibit)
    {
        // Nacheinander, damit ein schnelles Ein und Aus in der richtigen Reihenfolge ankommt.
        lock (_lock)
        {
            _pending = _pending.ContinueWith(_ => ApplyAsync(inhibit), TaskScheduler.Default).Unwrap();
        }
    }

    private async Task ApplyAsync(bool inhibit)
    {
        try
        {
            if (inhibit && _cookie is null)
            {
                _cookie = await CallAsync("Inhibit", "ss", writer =>
                {
                    writer.WriteString("just practise");
                    writer.WriteString("Practising");
                }, static (message, _) => message.GetBodyReader().ReadUInt32()).ConfigureAwait(false);
            }
            else if (!inhibit && _cookie is { } cookie)
            {
                _cookie = null;
                await CallAsync("UnInhibit", "u", writer => writer.WriteUInt32(cookie), static (_, _) => 0u).ConfigureAwait(false);
            }
        }
        catch (Exception exception) when (exception is DBusExceptionBase or InvalidOperationException)
        {
            // Kein Bildschirmschoner-Dienst oder keine Sitzung: nichts zu tun.
        }
    }

    private static Task<uint> CallAsync(string method, string signature, Action<MessageWriter> write, MessageValueReader<uint> read)
    {
        var connection = DBusConnection.Session ?? throw new InvalidOperationException("No D-Bus session.");
        MessageBuffer message;
        using (var writer = connection.GetMessageWriter())
        {
            writer.WriteMethodCallHeader(Service, ObjectPath, Service, method, signature, MessageFlags.None);
            write(writer);
            message = writer.CreateMessage();
        }

        return connection.CallMethodAsync(message, read, null);
    }
}
