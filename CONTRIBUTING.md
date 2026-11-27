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

1. checks Git, CMake and a C++ compiler (on Windows also MSYS2) and shows the install command for your system if something is missing,
2. builds the native libraries under `native/` if they are missing or their sources are newer (e.g. after a `git pull`),
3. builds the solution.

It never installs anything itself. Options:

```bash
dotnet run tools/setup.cs -- --check    # only check, build nothing
dotnet run tools/setup.cs -- --native   # rebuild the native libraries
dotnet run tools/setup.cs -- --strict   # fail if a native library cannot be built (used by CI)
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

Some audio parts are written in C or C++ and called via P/Invoke. Each folder under `native/` with a `CMakeLists.txt` builds one shared library `jp<folder>` into `native/bin/<rid>/`, for example `native/bin/linux-x64/libjpstretch.so`. Dependencies are fetched by CMake and pinned to a commit or a checksum.

- `native/stretch` (`jpstretch`): time-stretching with signalsmith-stretch (MIT).
- `native/aac` (`jpaac`): a small wrapper around a slim FFmpeg build that only reads AAC and ALAC in M4A/MP4 and `.aac` files. FFmpeg is LGPL, so it is built as separate shared libraries (`avformat`, `avcodec`, `avutil`) next to `jpaac`, never linked statically. The first build downloads the FFmpeg source and takes a minute or two.

Everything still builds without them; the app reports at runtime that a library is missing, and the tests that need it are skipped. Later CI will publish prebuilt libraries and the setup script will download them; a local build will still take precedence.

To build native code you need, in addition:

| System | Commands |
|---|---|
| Windows | `winget install --id Kitware.CMake -e`, the C++ build tools: `winget install --id Microsoft.VisualStudio.2022.BuildTools -e --override "--wait --passive --add Microsoft.VisualStudio.Workload.VCTools --includeRecommended"`, and for FFmpeg MSYS2: `winget install --id MSYS2.MSYS2 -e`, then `C:\msys64\usr\bin\bash.exe -lc "pacman -S --needed --noconfirm make diffutils"` |
| macOS | `xcode-select --install` and `brew install cmake` |
| Debian, Ubuntu | `sudo apt install cmake g++` |
| Fedora | `sudo dnf install cmake gcc-c++` |
| Arch | `sudo pacman -S cmake gcc` |

CMake 3.24 or newer is required. On Windows the setup script finds Visual Studio itself and sets up its build environment, so a normal terminal is enough. FFmpeg's `configure` runs in the MSYS2 bash and compiles with MSVC. To build by hand instead of with the script (on Windows from the "Developer PowerShell for VS"):

```bash
cmake -S native/stretch -B native/stretch/build
cmake --build native/stretch/build --config Release
cmake -S native/aac -B native/aac/build
cmake --build native/aac/build --config Release
```

If CMake on Windows reports "No CMAKE_CXX_COMPILER could be found", the C++ workload is missing: open the Visual Studio Installer and add "Desktop development with C++" to the Build Tools.

## Release packages

Packages are built with [Velopack](https://velopack.io/). Its command line tool `vpk` is a local .NET tool pinned in `dotnet-tools.json`; `dotnet tool restore` installs it. The native libraries must be built first. Then:

```bash
dotnet run tools/pack.cs -- --version 0.1.0-rc.1
```

This publishes the app self-contained with ReadyToRun and writes the packages for the current system to `artifacts/releases/<rid>/`: on Windows `Setup.exe` and a portable single exe (which does not update itself), on Linux an AppImage. `SHA256SUMS` lists the checksums (`sha256sum -c SHA256SUMS`). On Linux the AppImage needs `mksquashfs`: `sudo apt install squashfs-tools` (Debian, Ubuntu), `sudo dnf install squashfs-tools` (Fedora) or `sudo pacman -S squashfs-tools` (Arch).

The CI builds the packages for releases; building them locally is only needed to test packaging.

## Branches and pull requests

The project follows GitHub Flow: `main` is always releasable and only changes through pull requests.

- Work on a short-lived branch from `main`: `feature/<topic>`, `fix/<topic>`, `docs/<topic>` or `chore/<topic>`.
- A pull request can be merged once both CI builds pass. The branch is deleted after the merge.
- Pull requests are merged by rebase, so each commit lands on `main` on its own. Keep commits small and self-contained and clean them up before merging; squash is the exception for pull requests whose commits are not worth keeping one by one.
- Releases are tags on `main`. A release branch such as `release/0.1` is only created when an older version needs a fix while `main` has moved on.

## CI and releases

- `.github/workflows/ci.yml` builds the native libraries and the solution and runs all tests on Ubuntu 22.04, Windows and macOS, each on x64 and arm64 (macOS on Apple silicon only), for pushes to `main`, pull requests and by hand. Releases are built for x64 only so far.
- `.github/workflows/release.yml` runs for a version tag such as `v0.1.0-rc.1`. It tests and packs on both systems (downloading the previous release first so Velopack can build delta updates) and creates a **draft** GitHub release with all packages and a combined `SHA256SUMS`. Users and the update check see the version only after the draft is published by hand. A version with a suffix (`-rc.1`) becomes a pre-release.
- `.github/workflows/pages.yml` publishes the website in `site/` to GitHub Pages for changes to `site/` on `main` and for each published release. `{{version}}` in the pages becomes the tag of the newest published release, so the download links need no edit.
- `.github/dependabot.yml` checks the GitHub Actions used in the workflows once a month and opens pull requests for new versions.

## Third-party licenses

`THIRD-PARTY-NOTICES.txt` holds the full license text of every component shipped with the app; it is installed next to the app and shown under "About just practise". When you add a package or change a version, update that file too. A test checks that every package in `Directory.Packages.props` is listed.

## Project structure and conventions

- `src/JustPractise.Core`: domain logic without dependencies
- `src/JustPractise.Audio`: audio layer (engine, player, metronome, pitch detection, native interop)
- `src/JustPractise.Storage`: file system and persistence
- `src/JustPractise.App`: Avalonia UI (MVVM), organized by feature folders
- `tests/`: one xUnit project per library
- `native/`: C/C++ code built with CMake
- `prototypes/`: standalone test programs for the prototype checks, not part of the solution
- `tools/`: developer scripts
- `site/`: the website (plain HTML and CSS, English and German in `site/de/`), including the installation guide

Code style is enforced in the build via `.editorconfig` and the .NET analyzers. In short: classes are `sealed` by default, composition over inheritance, interfaces only at real boundaries, immutable records for data, constructor injection, time via `TimeProvider`, and no allocations or locks on the audio thread. The full rules are in the section "Weitere Festlegungen" of [docs/technologie.md](docs/technologie.md) (German).

The project never causes costs: no paid services, no ads, no donations. Please discuss larger changes in an issue before starting.
