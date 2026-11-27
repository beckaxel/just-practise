namespace JustPractise.App.Tests;

/// <summary>Ein leerer Ordner für einen Test, der danach gelöscht wird.</summary>
internal sealed class TemporaryDirectory : IDisposable
{
    public TemporaryDirectory()
    {
        // Kurz, weil SingleInstanceTests darin einen Unix-Socket anlegt: Unter macOS darf dessen Pfad höchstens
        // 104 Zeichen lang sein, und schon der temporäre Ordner (/var/folders/…/T/) hat rund 50.
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "jp-" + Guid.NewGuid().ToString("N")[..12]);
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public string File(string name) => System.IO.Path.Combine(Path, name);

    public string Create(string relativePath, string content = "")
    {
        var path = File(relativePath);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
        System.IO.File.WriteAllText(path, content);
        return path;
    }

    public void Dispose() => Directory.Delete(Path, recursive: true);
}
