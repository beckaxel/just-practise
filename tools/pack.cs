// Builds the release packages of just practise for this machine with Velopack (vpk, a local .NET tool).
// Publishes the app self-contained with ReadyToRun and packs it into artifacts/releases/<rid>/:
// on Windows Setup.exe and a portable single exe, on Linux an AppImage. SHA256SUMS lists the checksums.
//
//   dotnet run tools/pack.cs -- --version 0.1.0-rc.1
//
// The native library (dotnet run tools/setup.cs) must be built first; the CI runs the same steps.

using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

string? version = null;
for (var i = 0; i < args.Length; i++)
{
    if (args[i] == "--version" && i + 1 < args.Length)
    {
        version = args[++i].TrimStart('v');
    }
    else
    {
        version = null;
        break;
    }
}

if (version is null)
{
    Console.WriteLine("Usage: dotnet run tools/pack.cs -- --version 0.1.0-rc.1");
    return 2;
}

var root = FindRoot();
if (root is null)
{
    Console.WriteLine("Run this from inside the just-practise repository.");
    return 1;
}

var rid = RuntimeInformation.RuntimeIdentifier;
var publishDirectory = Path.Combine(root, "artifacts", "publish", rid);
var releaseDirectory = Path.Combine(root, "artifacts", "releases", rid);
foreach (var directory in new[] { publishDirectory, releaseDirectory })
{
    if (Directory.Exists(directory))
    {
        Directory.Delete(directory, recursive: true);
    }
}

string[] publish =
[
    "publish", Path.Combine("src", "JustPractise.App"), "-c", "Release", "-r", rid, "--self-contained",
    $"-p:Version={version}", "-o", publishDirectory,
];
if (Run(root, "dotnet", publish) != 0)
{
    Console.WriteLine("Publishing failed.");
    return 1;
}

var nativeLibrary = OperatingSystem.IsWindows() ? "jpstretch.dll" : OperatingSystem.IsMacOS() ? "libjpstretch.dylib" : "libjpstretch.so";
if (!File.Exists(Path.Combine(publishDirectory, nativeLibrary)))
{
    Console.WriteLine($"{nativeLibrary} is missing. Build it first with: dotnet run tools/setup.cs");
    return 1;
}

var icon = Path.Combine(root, "src", "JustPractise.App", "Assets", OperatingSystem.IsWindows() ? "icon.ico" : "icon.png");
string[] pack =
[
    "vpk", "pack",
    "--packId", "just-practise",
    "--packTitle", "just practise",
    "--packAuthors", "just practise contributors",
    "--packVersion", version,
    "--packDir", publishDirectory,
    "--mainExe", OperatingSystem.IsWindows() ? "just-practise.exe" : "just-practise",
    "--icon", icon,
    // Die eigenen Symbole kommen mit (Zeilennummern in Fehlerberichten); die aus NuGet-Paketen lässt das App-Projekt weg.
    "--exclude", "(?!)",
    "--outputDir", releaseDirectory,
    .. OperatingSystem.IsLinux() ? new[] { "--categories", "AudioVideo;Audio;Music" } : [],
    // Unter Windows ersetzt eine einzelne exe das ZIP von Velopack (siehe unten).
    .. OperatingSystem.IsWindows() ? new[] { "--noPortable" } : [],
];
if (Run(root, "dotnet", pack) != 0)
{
    Console.WriteLine("Packing failed.");
    return 1;
}

// Portable Variante unter Windows: eine einzelne exe. Die nativen Bibliotheken entpackt .NET beim ersten Start
// in einen Cache unter %TEMP%. Sie ist keine Velopack-Installation und aktualisiert sich daher nicht selbst.
if (OperatingSystem.IsWindows())
{
    var portableDirectory = Path.Combine(root, "artifacts", "publish", rid + "-portable");
    if (Directory.Exists(portableDirectory))
    {
        Directory.Delete(portableDirectory, recursive: true);
    }

    string[] portable =
    [
        .. publish[..^1], portableDirectory,
        "-p:PublishSingleFile=true", "-p:IncludeNativeLibrariesForSelfExtract=true", "-p:EnableCompressionInSingleFile=true",
        "-p:DebugType=embedded", // Symbole in der exe, für Zeilennummern in Fehlerberichten
    ];
    if (Run(root, "dotnet", portable) != 0)
    {
        Console.WriteLine("Publishing the portable exe failed.");
        return 1;
    }

    File.Copy(Path.Combine(portableDirectory, "just-practise.exe"), Path.Combine(releaseDirectory, "just-practise-win-Portable.exe"));
}

// Prüfsummen für alle Dateien des Releases, im Format von sha256sum (sha256sum -c SHA256SUMS).
var sums = new StringBuilder();
foreach (var file in Directory.GetFiles(releaseDirectory).Order(StringComparer.Ordinal))
{
    using var stream = File.OpenRead(file);
    sums.Append(Convert.ToHexStringLower(SHA256.HashData(stream))).Append("  ").Append(Path.GetFileName(file)).Append('\n');
}

File.WriteAllText(Path.Combine(releaseDirectory, "SHA256SUMS"), sums.ToString());

Console.WriteLine();
foreach (var file in new DirectoryInfo(releaseDirectory).GetFiles().OrderBy(file => file.Name, StringComparer.Ordinal))
{
    Console.WriteLine($"{file.Length / 1024.0 / 1024.0,8:F1} MB  {file.Name}");
}

Console.WriteLine($"Packages in {releaseDirectory}");
return 0;

static int Run(string workingDirectory, string file, string[] arguments)
{
    Console.WriteLine($"> {file} {string.Join(' ', arguments)}");
    try
    {
        using var process = Process.Start(new ProcessStartInfo(file, arguments) { WorkingDirectory = workingDirectory, UseShellExecute = false });
        if (process is null)
        {
            return -1;
        }

        process.WaitForExit();
        return process.ExitCode;
    }
    catch (Win32Exception exception)
    {
        Console.WriteLine(exception.Message);
        return -1;
    }
}

static string? FindRoot()
{
    for (var directory = new DirectoryInfo(Environment.CurrentDirectory); directory is not null; directory = directory.Parent)
    {
        if (File.Exists(Path.Combine(directory.FullName, "JustPractise.slnx")))
        {
            return directory.FullName;
        }
    }

    return null;
}
