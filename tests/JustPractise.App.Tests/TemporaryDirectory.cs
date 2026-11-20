namespace JustPractise.App.Tests;

/// <summary>Ein leerer Ordner für einen Test, der danach gelöscht wird.</summary>
internal sealed class TemporaryDirectory : IDisposable
{
    public TemporaryDirectory()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "jp-tests-" + Guid.NewGuid().ToString("N"));
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
