using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace JustPractise.App.Shell;

/// <summary>
/// Sorgt dafür, dass die App nur einmal läuft. Die erste Instanz hält eine Sperrdatei und wartet an einem lokalen
/// Socket. Eine weitere Instanz bittet sie dort, ihr Fenster nach vorn zu holen, und beendet sich dann.
/// </summary>
/// <remarks>
/// Die Sperrdatei (exklusiv geöffnet, unter Linux mit <c>flock</c>) entscheidet, wer die erste Instanz ist; das ist
/// auch bei zwei gleichzeitigen Starts eindeutig. Sie wird beim Beenden, auch beim Absturz, vom System freigegeben.
/// Der Socket (Unix Domain Socket, unter Windows 10 und neuer ebenso) dient nur der Nachricht „nach vorn holen“.
/// </remarks>
public sealed partial class SingleInstance : IDisposable
{
    private const byte ActivateMessage = 0x41;
    private const int AnyProcess = -1;

    private readonly FileStream? _lock;
    private readonly Socket? _listener;
    private readonly CancellationTokenSource _stop = new();

    private SingleInstance(FileStream? lockFile, Socket? listener)
    {
        _lock = lockFile;
        _listener = listener;
    }

    /// <summary>Kommt, wenn eine weitere Instanz gestartet wurde; auf einem Hintergrund-Thread.</summary>
    public event EventHandler? ActivationRequested;

    /// <summary>
    /// Der Ordner für Sperrdatei und Socket: unter Linux <c>$XDG_RUNTIME_DIR</c> (pro Nutzer, im Arbeitsspeicher),
    /// sonst der temporäre Ordner des Nutzers.
    /// </summary>
    public static string DefaultDirectory => Path.Combine(
        Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR") is { Length: > 0 } runtime ? runtime : Path.GetTempPath(),
        "just-practise");

    /// <summary>
    /// Wird zur ersten Instanz oder holt die laufende nach vorn. Gibt die erste Instanz zurück, sonst <c>null</c>;
    /// dann soll sich die App beenden.
    /// </summary>
    /// <param name="timeout">
    /// Wie lange eine weitere Instanz es versucht, z. B. wenn die erste gerade beendet wird oder noch startet.
    /// Danach startet sie doch, damit die App nie gar nicht aufgeht.
    /// </param>
    public static SingleInstance? Start(string directory, TimeSpan timeout)
    {
        Directory.CreateDirectory(directory);
        var lockPath = Path.Combine(directory, "instance.lock");
        var socketPath = Path.Combine(directory, "instance.sock");
        var deadline = DateTime.UtcNow + timeout;
        while (true)
        {
            if (TryLock(lockPath) is { } lockFile)
            {
                return Listen(lockFile, socketPath);
            }

            if (TryActivate(socketPath))
            {
                return null;
            }

            if (DateTime.UtcNow >= deadline)
            {
                // Ohne Sperre weiterlaufen ist besser, als gar nicht zu starten.
                return new SingleInstance(lockFile: null, listener: null);
            }

            Thread.Sleep(100);
        }
    }

    public void Dispose()
    {
        _stop.Cancel();
        _listener?.Dispose();
        _lock?.Dispose();
        _stop.Dispose();
    }

    private static SingleInstance Listen(FileStream lockFile, string socketPath)
    {
        // Ein Socket von einer abgestürzten Instanz bleibt liegen; wer die Sperre hat, darf ihn ersetzen.
        File.Delete(socketPath);
        var listener = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        try
        {
            listener.Bind(new UnixDomainSocketEndPoint(socketPath));
            listener.Listen();
        }
        catch (Exception exception) when (exception is SocketException or ArgumentOutOfRangeException)
        {
            // Ohne Socket läuft die App trotzdem; eine zweite Instanz startet dann nach dem Timeout selbst.
            // ArgumentOutOfRangeException: Der Pfad ist zu lang für einen Socket (unter Linux höchstens 108 Zeichen).
            listener.Dispose();
            return new SingleInstance(lockFile, listener: null);
        }

        var instance = new SingleInstance(lockFile, listener);
        _ = Task.Run(instance.AcceptAsync);
        return instance;
    }

    private async Task AcceptAsync()
    {
        var listener = _listener!;
        var buffer = new byte[1];
        while (!_stop.IsCancellationRequested)
        {
            try
            {
                using var client = await listener.AcceptAsync(_stop.Token).ConfigureAwait(false);
                var read = await client.ReceiveAsync(buffer, SocketFlags.None, _stop.Token).ConfigureAwait(false);
                if (read == 1 && buffer[0] == ActivateMessage)
                {
                    ActivationRequested?.Invoke(this, EventArgs.Empty);
                }
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (ObjectDisposedException)
            {
                return;
            }
            catch (SocketException) when (!listener.IsBound)
            {
                return;
            }
            catch (SocketException)
            {
                // Eine abgebrochene Verbindung betrifft nur diese eine Anfrage.
            }
        }
    }

    private static FileStream? TryLock(string path)
    {
        try
        {
            return new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static bool TryActivate(string socketPath)
    {
        if (OperatingSystem.IsWindows())
        {
            // Die neue Instanz wurde gerade vom Nutzer gestartet und darf deshalb das Fenster der ersten Instanz
            // nach vorn holen lassen; ohne diese Erlaubnis blinkt es unter Windows nur in der Taskleiste.
            _ = AllowSetForegroundWindow(AnyProcess);
        }

        try
        {
            using var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
            socket.Connect(new UnixDomainSocketEndPoint(socketPath));
            socket.Send([ActivateMessage]);
            return true;
        }
        catch (Exception exception) when (exception is SocketException or ArgumentOutOfRangeException)
        {
            return false;
        }
    }

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool AllowSetForegroundWindow(int processId);
}
