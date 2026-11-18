using System.Text;
using JustPractise.Core.DataFiles;

namespace JustPractise.Storage;

/// <summary>
/// Liest und schreibt eine Datendatei (Sidecar, Einstellungen, Gedächtnis) auf der Platte.
/// </summary>
/// <remarks>
/// Blockiert, weil Dateien in Cloud-Ordnern erst geladen werden müssen. Nicht vom UI-Thread aus aufrufen.
/// Geschrieben wird nur, wenn die Datei beim letzten <see cref="Load"/> fehlte oder lesbar war und
/// nicht von einer neueren App-Version stammt. Fehler beim Schreiben werden als <see cref="IOException"/>
/// oder <see cref="UnauthorizedAccessException"/> weitergereicht.
/// </remarks>
public sealed class DataFileStore<T>(string path, DataFile<T> format)
    where T : class, new()
{
    private static readonly UTF8Encoding Utf8WithoutBom = new(encoderShouldEmitUTF8Identifier: false);

    private readonly Lock _lock = new();
    private DataFileStatus? _status;

    public string Path { get; } = path;

    /// <summary>Liest die Datei. Fehlt sie, gelten Standardwerte, und sie darf angelegt werden.</summary>
    public DataFileResult<T> Load()
    {
        lock (_lock)
        {
            DataFileResult<T> result;
            try
            {
                result = File.Exists(Path)
                    ? format.Read(File.ReadAllText(Path, Encoding.UTF8))
                    : new DataFileResult<T>(new T(), DataFileStatus.Ok);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // Nicht lesbar, z. B. Cloud-Laufwerk nicht eingebunden: wie beschädigt behandeln.
                result = new DataFileResult<T>(new T(), DataFileStatus.Corrupt);
            }

            _status = result.Status;
            return result;
        }
    }

    /// <summary>
    /// Schreibt die Datei atomar: erst in eine temporäre Datei im selben Ordner, dann umbenennen.
    /// Liefert <c>false</c>, wenn die Datei nicht überschrieben werden darf.
    /// </summary>
    public bool TrySave(T value)
    {
        lock (_lock)
        {
            if (_status is null)
            {
                throw new InvalidOperationException($"{nameof(Load)} must be called before saving {Path}.");
            }

            if (_status != DataFileStatus.Ok)
            {
                return false;
            }

            var directory = System.IO.Path.GetDirectoryName(Path) ?? ".";
            Directory.CreateDirectory(directory);
            var temporary = System.IO.Path.Combine(directory, $".{System.IO.Path.GetFileName(Path)}.{Guid.NewGuid():N}.tmp");
            try
            {
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    stream.Write(Utf8WithoutBom.GetBytes(format.Write(value)));
                    stream.Flush(flushToDisk: true);
                }

                File.Move(temporary, Path, overwrite: true);
                return true;
            }
            finally
            {
                File.Delete(temporary);
            }
        }
    }
}
