// Sets up a development environment for just practise on Windows, Linux and macOS.
// The only prerequisite is the .NET SDK; everything else is checked and explained.
// The script never installs anything itself.
//
//   dotnet run tools/setup.cs              check tools, build missing native libraries, build the solution
//   dotnet run tools/setup.cs -- --check   only check tools and native libraries
//   dotnet run tools/setup.cs -- --native  rebuild the native libraries even if they exist
//   dotnet run tools/setup.cs -- --strict  fail if a native library cannot be built (used by CI)
//
// On Windows the script sets up the Visual Studio build environment (vcvarsall) for CMake by itself.
// Native projects that build a library with configure (a *.sh next to CMakeLists.txt, e.g. FFmpeg in native/aac)
// also need MSYS2 there; without it they are skipped and the app runs without them.

using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

string[] knownOptions = ["--check", "--native", "--strict"];
var unknown = args.Except(knownOptions).ToList();
if (unknown.Count > 0)
{
    Console.WriteLine($"Unknown option {unknown[0]}.");
    Console.WriteLine("Usage: dotnet run tools/setup.cs [-- --check | --native | --strict]");
    return 2;
}

var checkOnly = args.Contains("--check");
var forceNative = args.Contains("--native");
var strict = args.Contains("--strict");

var root = Repository.FindRoot();
if (root is null)
{
    Console.WriteLine("Run this from inside the just-practise repository.");
    return 1;
}

var platform = Platform.Detect();
Console.WriteLine($"just practise setup on {platform.Name} ({platform.Rid})");
Console.WriteLine();

// Tools
var missing = new List<Requirement>();
Report.Line(true, ".NET SDK", Shell.FirstLine("dotnet", "--version") ?? "?");

var git = Shell.FirstLine("git", "--version");
Report.Line(git is not null, "Git", git ?? "not found");
if (git is null)
{
    missing.Add(Requirement.Git);
}

var cmake = CMake.Find();
Report.Line(cmake.IsUsable, "CMake", cmake.Description);
if (!cmake.IsUsable)
{
    missing.Add(Requirement.CMake);
}

var compiler = platform.FindCompiler();
Report.Line(compiler is not null, "C++ compiler", compiler ?? "not found");
if (compiler is null)
{
    missing.Add(Requirement.Compiler);
}

var nativeProjects = NativeProject.FindAll(root, platform);
var msys2 = Msys2.Find();
var needsMsys2 = platform.Os == OSPlatform.Windows && nativeProjects.Any(project => project.NeedsUnixShell);
if (needsMsys2)
{
    Report.Line(msys2.IsUsable, "MSYS2", msys2.Description);
}

// Native libraries
Console.WriteLine();
var toBuild = nativeProjects.Where(project => forceNative || !project.IsUpToDate).ToList();
foreach (var project in nativeProjects)
{
    var state = project switch
    {
        { IsBuilt: false } => "not built",
        { IsUpToDate: false } => "outdated, sources are newer",
        _ => Path.GetRelativePath(root, project.OutputPath),
    };
    Report.Line(project.IsUpToDate, $"native/{project.Name}", state);
}

var skipped = needsMsys2 && !msys2.IsUsable
    ? toBuild.Where(project => project.NeedsUnixShell).ToList()
    : [];
if (skipped.Count > 0)
{
    Console.WriteLine();
    Console.WriteLine($"{string.Join(", ", skipped.Select(project => $"native/{project.Name}"))} needs MSYS2 on Windows. To install it:");
    foreach (var hint in Msys2.InstallHints)
    {
        Console.WriteLine($"  {hint}");
    }

    toBuild = toBuild.Except(skipped).ToList();
}

if (missing.Count > 0)
{
    Console.WriteLine();
    if (cmake.IsTooOld)
    {
        Console.WriteLine($"CMake {cmake.Version} is too old, {CMake.MinimumVersion} or newer is needed: https://cmake.org/download/");
    }

    var hints = platform.InstallHints(missing.Where(requirement => requirement != Requirement.CMake || !cmake.IsTooOld));
    if (hints.Count > 0)
    {
        Console.WriteLine("To install the missing tools:");
        foreach (var hint in hints)
        {
            Console.WriteLine($"  {hint}");
        }

        Console.WriteLine("Then open a new terminal and run this script again.");
    }
}

if (checkOnly)
{
    return missing.Count == 0 && toBuild.Count == 0 && skipped.Count == 0 ? 0 : 1;
}

if (strict && (missing.Count > 0 || skipped.Count > 0))
{
    Console.WriteLine();
    Console.WriteLine("--strict: all native libraries must be built, but tools are missing.");
    return 1;
}

if (toBuild.Count > 0)
{
    Console.WriteLine();
    if (missing.Count > 0)
    {
        // The app builds and runs without them, so this is no error.
        Console.WriteLine("Skipping the native libraries because tools are missing.");
        Console.WriteLine("The app runs without them, but without tempo changes (stretch) and M4A files (aac).");
    }
    else
    {
        var environment = platform.Os == OSPlatform.Windows ? VisualStudio.BuildEnvironment(platform) : null;
        foreach (var project in toBuild)
        {
            if (!project.Build(environment))
            {
                Console.WriteLine($"Building native/{project.Name} failed.");
                return 1;
            }
        }
    }
}

Console.WriteLine();
if (Shell.Run(root, "dotnet", "build") != 0)
{
    Console.WriteLine("dotnet build failed.");
    return 1;
}

Console.WriteLine();
Console.WriteLine("Done. Next steps:");
Console.WriteLine("  dotnet test                                 run all tests");
Console.WriteLine("  dotnet run --project src/JustPractise.App   start the app");
return 0;

internal enum Requirement
{
    Git,
    CMake,
    Compiler,
}

internal static class Repository
{
    public static string? FindRoot()
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
}

internal static class Report
{
    public static void Line(bool ok, string name, string detail) =>
        Console.WriteLine($"  {(ok ? "[ok]     " : "[missing]")} {name,-20} {detail}");
}

internal static class Shell
{
    /// <summary>Runs a command quietly and returns the first line of its output, or null if it fails.</summary>
    public static string? FirstLine(string file, params string[] arguments)
    {
        var info = new ProcessStartInfo(file, arguments)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        try
        {
            using var process = Process.Start(info);
            if (process is null)
            {
                return null;
            }

            var output = process.StandardOutput.ReadToEndAsync();
            var error = process.StandardError.ReadToEndAsync();
            process.WaitForExit();
            if (process.ExitCode != 0)
            {
                return null;
            }

            var text = output.Result.Length > 0 ? output.Result : error.Result;
            return text.Split('\n').Select(line => line.Trim()).FirstOrDefault(line => line.Length > 0);
        }
        catch (Win32Exception)
        {
            // The program is not installed.
            return null;
        }
    }

    /// <summary>Runs a command with its output shown in the console and returns the exit code.</summary>
    public static int Run(string workingDirectory, string file, params string[] arguments) =>
        Run(workingDirectory, null, file, arguments);

    /// <summary>Like <see cref="Run(string, string, string[])"/>, with these environment variables instead of the current ones.</summary>
    public static int Run(string workingDirectory, IReadOnlyDictionary<string, string>? environment, string file, params string[] arguments)
    {
        Console.WriteLine($"> {file} {string.Join(' ', arguments)}");
        var info = new ProcessStartInfo(file, arguments)
        {
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
        };
        if (environment is not null)
        {
            info.Environment.Clear();
            foreach (var (name, value) in environment)
            {
                info.Environment[name] = value;
            }
        }

        try
        {
            using var process = Process.Start(info);
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
}

internal sealed record CMake(Version? Version)
{
    public static readonly Version MinimumVersion = new(3, 24);

    public bool IsUsable => Version is not null && Version >= MinimumVersion;

    public bool IsTooOld => Version is not null && Version < MinimumVersion;

    public string Description => Version switch
    {
        null => "not found",
        _ when IsTooOld => $"{Version} (too old, {MinimumVersion} or newer needed)",
        _ => Version.ToString(),
    };

    public static CMake Find()
    {
        var line = Shell.FirstLine("cmake", "--version");
        var match = line is null ? null : Regex.Match(line, @"(\d+)\.(\d+)(?:\.(\d+))?");
        return new CMake(match is { Success: true } && Version.TryParse(match.Value, out var version) ? version : null);
    }
}

internal sealed record NativeProject(string Name, string SourceDirectory, string OutputPath)
{
    public bool IsBuilt => File.Exists(OutputPath);

    /// <summary>Built, and no source file (src/, include/, files next to CMakeLists.txt) is newer, e.g. after a git pull.</summary>
    public bool IsUpToDate => IsBuilt && SourceFiles().All(file => File.GetLastWriteTimeUtc(file) <= File.GetLastWriteTimeUtc(OutputPath));

    /// <summary>Builds a library with configure and make (a shell script next to CMakeLists.txt); needs MSYS2 on Windows.</summary>
    public bool NeedsUnixShell => Directory.EnumerateFiles(SourceDirectory, "*.sh").Any();

    private static readonly string[] SourceFolders = ["src", "include"];

    private IEnumerable<string> SourceFiles() =>
        SourceFolders
            .Select(folder => Path.Combine(SourceDirectory, folder))
            .Where(Directory.Exists)
            .SelectMany(folder => Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories))
            .Concat(Directory.EnumerateFiles(SourceDirectory));

    /// <summary>Every folder under native/ with a CMakeLists.txt builds a library named jp&lt;folder&gt;.</summary>
    public static List<NativeProject> FindAll(string root, Platform platform)
    {
        var native = Path.Combine(root, "native");
        if (!Directory.Exists(native))
        {
            return [];
        }

        return Directory.GetDirectories(native)
            .Where(directory => File.Exists(Path.Combine(directory, "CMakeLists.txt")))
            .Order(StringComparer.Ordinal)
            .Select(directory =>
            {
                var name = Path.GetFileName(directory);
                var output = Path.Combine(native, "bin", platform.Rid, platform.LibraryFileName($"jp{name}"));
                return new NativeProject(name, directory, output);
            })
            .ToList();
    }

    /// <param name="environment">environment for CMake, e.g. the Visual Studio build environment; null keeps the current one</param>
    public bool Build(IReadOnlyDictionary<string, string>? environment)
    {
        var buildDirectory = Path.Combine(SourceDirectory, "build");
        return Shell.Run(SourceDirectory, environment, "cmake", "-S", SourceDirectory, "-B", buildDirectory) == 0
            && Shell.Run(SourceDirectory, environment, "cmake", "--build", buildDirectory, "--config", "Release") == 0;
    }
}

internal sealed record Platform(OSPlatform Os, string Name, string Rid, string? LinuxFamily)
{
    private static readonly string[] UnixCompilers = ["c++", "g++", "clang++"];

    public static Platform Detect()
    {
        var arch = RuntimeInformation.OSArchitecture == Architecture.Arm64 ? "arm64" : "x64";
        if (OperatingSystem.IsWindows())
        {
            return new Platform(OSPlatform.Windows, "Windows", $"win-{arch}", null);
        }

        if (OperatingSystem.IsMacOS())
        {
            return new Platform(OSPlatform.OSX, "macOS", $"osx-{arch}", null);
        }

        var (name, family) = ReadOsRelease();
        return new Platform(OSPlatform.Linux, name, $"linux-{arch}", family);
    }

    public string LibraryFileName(string name) =>
        Os == OSPlatform.Windows ? $"{name}.dll"
        : Os == OSPlatform.OSX ? $"lib{name}.dylib"
        : $"lib{name}.so";

    public string? FindCompiler()
    {
        if (Os == OSPlatform.Windows)
        {
            return FindVisualStudio();
        }

        if (Os == OSPlatform.OSX && Shell.FirstLine("xcode-select", "-p") is null)
        {
            return null;
        }

        return UnixCompilers
            .Select(compiler => Shell.FirstLine(compiler, "--version"))
            .FirstOrDefault(version => version is not null);
    }

    public List<string> InstallHints(IEnumerable<Requirement> missing)
    {
        var requirements = missing.ToList();
        if (requirements.Count == 0)
        {
            return [];
        }

        if (Os == OSPlatform.Windows)
        {
            return requirements.Select(requirement => requirement switch
            {
                Requirement.Git => "winget install --id Git.Git -e",
                Requirement.CMake => "winget install --id Kitware.CMake -e",
                _ => "winget install --id Microsoft.VisualStudio.2022.BuildTools -e --override \"--wait --passive --add Microsoft.VisualStudio.Workload.VCTools --includeRecommended\"",
            }).ToList();
        }

        if (Os == OSPlatform.OSX)
        {
            // The Xcode command line tools bring Git and the compiler; CMake comes from Homebrew (https://brew.sh).
            return requirements.Select(requirement => requirement == Requirement.CMake ? "brew install cmake" : "xcode-select --install")
                .Distinct()
                .ToList();
        }

        (string Command, string Git, string CMake, string Compiler)? packages = LinuxFamily switch
        {
            "debian" => ("sudo apt install", "git", "cmake", "g++"),
            "fedora" => ("sudo dnf install", "git", "cmake", "gcc-c++"),
            "arch" => ("sudo pacman -S", "git", "cmake", "gcc"),
            "suse" => ("sudo zypper install", "git", "cmake", "gcc-c++"),
            _ => null,
        };

        if (packages is not { } p)
        {
            return ["Install Git, CMake (3.24 or newer) and a C++17 compiler (GCC or Clang) with your package manager."];
        }

        var names = requirements.Select(requirement => requirement switch
        {
            Requirement.Git => p.Git,
            Requirement.CMake => p.CMake,
            _ => p.Compiler,
        });
        return [$"{p.Command} {string.Join(' ', names)}"];
    }

    private string? FindVisualStudio() => VisualStudio.Find(this, "displayName");

    /// <summary>Reads the distribution name and maps it to a package manager family.</summary>
    private static (string Name, string? Family) ReadOsRelease()
    {
        const string path = "/etc/os-release";
        if (!File.Exists(path))
        {
            return ("Linux", null);
        }

        var values = File.ReadAllLines(path)
            .Select(line => line.Split('=', 2))
            .Where(parts => parts.Length == 2)
            .ToDictionary(parts => parts[0], parts => parts[1].Trim('"'), StringComparer.Ordinal);

        var name = values.GetValueOrDefault("PRETTY_NAME") ?? "Linux";
        var ids = $"{values.GetValueOrDefault("ID")} {values.GetValueOrDefault("ID_LIKE")}"
            .Split(' ', StringSplitOptions.RemoveEmptyEntries);
        string[] families = ["debian", "fedora", "arch", "suse"];
        var family = families.FirstOrDefault(f => ids.Any(id => id == f || id.Contains(f, StringComparison.Ordinal)
            || (f == "debian" && id == "ubuntu") || (f == "fedora" && id is "rhel" or "centos")));
        return (name, family);
    }
}

/// <summary>Visual Studio with the C++ tools, found via vswhere.</summary>
internal static class VisualStudio
{
    public static string? Find(Platform platform, string property)
    {
        var vswhere = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            "Microsoft Visual Studio", "Installer", "vswhere.exe");
        if (!File.Exists(vswhere))
        {
            return null;
        }

        var component = IsArm64(platform)
            ? "Microsoft.VisualStudio.Component.VC.Tools.ARM64"
            : "Microsoft.VisualStudio.Component.VC.Tools.x86.x64";
        return Shell.FirstLine(vswhere, "-latest", "-products", "*", "-requires", component, "-property", property);
    }

    /// <summary>
    /// The environment of the Visual Studio developer prompt (vcvarsall), so that cl.exe and link.exe are on the PATH
    /// for tools that CMake starts, such as configure for FFmpeg. Null if it is already set up or cannot be found.
    /// </summary>
    public static Dictionary<string, string>? BuildEnvironment(Platform platform)
    {
        if (Shell.FirstLine("where", "cl.exe") is not null)
        {
            return null;
        }

        var installation = Find(platform, "installationPath");
        var vcvarsall = installation is null ? null : Path.Combine(installation, "VC", "Auxiliary", "Build", "vcvarsall.bat");
        if (vcvarsall is null || !File.Exists(vcvarsall))
        {
            return null;
        }

        var info = new ProcessStartInfo("cmd.exe")
        {
            // cmd needs the quotes exactly like this; ProcessStartInfo would escape them in an argument list.
            Arguments = $"/s /c \"\"{vcvarsall}\" {(IsArm64(platform) ? "arm64" : "x64")} >nul && set\"",
            RedirectStandardOutput = true,
            UseShellExecute = false,
        };
        using var process = Process.Start(info);
        if (process is null)
        {
            return null;
        }

        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            return null;
        }

        var environment = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var separator = line.IndexOf('=', StringComparison.Ordinal);
            if (separator > 0)
            {
                environment[line[..separator]] = line[(separator + 1)..];
            }
        }

        return environment;
    }

    private static bool IsArm64(Platform platform) => platform.Rid.EndsWith("arm64", StringComparison.Ordinal);
}

/// <summary>MSYS2 with make and cmp, needed on Windows to run configure for FFmpeg.</summary>
internal sealed record Msys2(string Root, bool HasBash, bool HasTools)
{
    public static readonly string[] InstallHints =
    [
        "winget install --id MSYS2.MSYS2 -e",
        @"C:\msys64\usr\bin\bash.exe -lc ""pacman -S --needed --noconfirm make diffutils""",
    ];

    public bool IsUsable => HasBash && HasTools;

    public string Description => this switch
    {
        { HasBash: false } => $"not found in {Root}",
        { HasTools: false } => "make or diffutils missing",
        _ => Root,
    };

    public static Msys2 Find()
    {
        const string root = @"C:\msys64";
        var bin = Path.Combine(root, "usr", "bin");
        return new Msys2(root, File.Exists(Path.Combine(bin, "bash.exe")),
            File.Exists(Path.Combine(bin, "make.exe")) && File.Exists(Path.Combine(bin, "cmp.exe")));
    }
}
