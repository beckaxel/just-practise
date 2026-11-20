# Contributing

Thanks for your interest in just practise. This guide gets you from a fresh machine to a running build on Linux, Windows or macOS.

## 1. Install the .NET SDK and Git

The project needs the .NET 10 SDK, version 10.0.404 or newer (pinned in `global.json`).

| System | Commands |
|---|---|
| Windows | `winget install --id Microsoft.DotNet.SDK.10 -e` and `winget install --id Git.Git -e` |
| macOS | `xcode-select --install` (brings Git), then the installer from [dot.net](https://dotnet.microsoft.com/download/dotnet/10.0) or `brew install --cask dotnet-sdk` |
| Debian, Ubuntu | `sudo apt install git`, .NET as described in [Install .NET on Linux](https://learn.microsoft.com/dotnet/core/install/linux) (Microsoft package repository or the distribution's packages) |
| Fedora | `sudo dnf install git dotnet-sdk-10.0` |
| Arch | `sudo pacman -S git dotnet-sdk` |

Open a new terminal afterwards and check with `dotnet --version`.

## 2. Clone and run the setup script

```bash
git clone https://github.com/beckaxel/just-practise.git
cd just-practise
dotnet run tools/setup.cs
```

The script is a plain C# file and works the same on every system. It

1. checks Git, CMake and a C++ compiler and shows the install command for your system if something is missing,
2. builds the native libraries under `native/` if they are missing or their sources are newer (e.g. after a `git pull`),
3. builds the solution.

It never installs anything itself. Options:

```bash
dotnet run tools/setup.cs -- --check    # only check, build nothing
dotnet run tools/setup.cs -- --native   # rebuild the native libraries
```

## 3. Build, test, run

```bash
dotnet build                                   # warnings are errors
dotnet test                                    # all tests (xUnit v3 on Microsoft Testing Platform)
dotnet test --project tests/JustPractise.Core.Tests
dotnet run --project src/JustPractise.App      # start the app
```

To listen to the audio layer (playback, tempo, metronome) without the app, play a file in the terminal; it opens the default output device:

```bash
dotnet run tools/play.cs -- <file> [--tempo 0.8] [--bpm 100] [--meter 3/4]
```

To measure startup time and package size, publish the app with and without ReadyToRun and start each variant a few times; the app opens its window and the audio device, plays nothing and closes after the first frame. The report goes to `artifacts/startup/report.md`:

```bash
dotnet run tools/startup.cs
```

To run single tests, pass a filter after `--`, for example `dotnet test --project tests/JustPractise.Core.Tests -- --filter-method "*Grouping*"`.

Any editor works. Free options with good C# and Avalonia support are VS Code with the C# Dev Kit, Visual Studio Community (Windows) and JetBrains Rider (free for non-commercial use).

## Native code

Some audio parts (time-stretching) are written in C++ and called via P/Invoke. Each folder under `native/` with a `CMakeLists.txt` builds one shared library `jp<folder>` into `native/bin/<rid>/`, for example `native/bin/linux-x64/libjpstretch.so`. Dependencies are fetched by CMake and pinned to a commit.

The audio layer uses `jpstretch` for the player. Everything still builds without it; the player reports at runtime that the library is missing, and the tests that need it are skipped. Later CI will publish prebuilt libraries and the setup script will download them; a local build will still take precedence.

To build native code you need, in addition:

| System | Commands |
|---|---|
| Windows | `winget install --id Kitware.CMake -e` and the C++ build tools: `winget install --id Microsoft.VisualStudio.2022.BuildTools -e --override "--wait --passive --add Microsoft.VisualStudio.Workload.VCTools --includeRecommended"` |
| macOS | `xcode-select --install` and `brew install cmake` |
| Debian, Ubuntu | `sudo apt install cmake g++` |
| Fedora | `sudo dnf install cmake gcc-c++` |
| Arch | `sudo pacman -S cmake gcc` |

CMake 3.24 or newer is required. To build by hand instead of with the script:

```bash
cmake -S native/stretch -B native/stretch/build
cmake --build native/stretch/build --config Release
```

If CMake on Windows reports "No CMAKE_CXX_COMPILER could be found", the C++ workload is missing: open the Visual Studio Installer and add "Desktop development with C++" to the Build Tools.

## Project structure and conventions

- `src/JustPractise.Core`: domain logic without dependencies
- `src/JustPractise.Audio`: audio layer (engine, player, metronome, pitch detection, native interop)
- `src/JustPractise.Storage`: file system and persistence
- `src/JustPractise.App`: Avalonia UI (MVVM), organized by feature folders
- `tests/`: one xUnit project per library
- `native/`: C/C++ code built with CMake
- `prototypes/`: standalone test programs for the prototype checks, not part of the solution
- `tools/`: developer scripts

Code style is enforced in the build via `.editorconfig` and the .NET analyzers. In short: classes are `sealed` by default, composition over inheritance, interfaces only at real boundaries, immutable records for data, constructor injection, time via `TimeProvider`, and no allocations or locks on the audio thread. The full rules are in the section "Weitere Festlegungen" of [docs/technologie.md](docs/technologie.md) (German).

The project never causes costs: no paid services, no ads, no donations. Please discuss larger changes in an issue before starting.
