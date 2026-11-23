# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Repository state

The concept phase and the prototype are complete; the first release candidate `v0.1.0-rc.1` is tagged on `main`. Work follows GitHub Flow: short-lived branches (`feature/`, `fix/`, `docs/`, `chore/`) from `main`, pull requests that need both CI builds green, merged by rebase (squash only as the exception), releases as tags on `main` (see `CONTRIBUTING.md`). The concept documents (in German) are the source of truth for features and technical decisions:

- `docs/funktionen.md`: the feature concept (audio player, practice timer, metronome, tuner, keyboard shortcuts, language and music notation, app settings)
- `docs/technologie.md`: technology decision, libraries, project structure, code conventions, file formats, translations, packaging, signing and the prototype checks
- `docs/roadmap.md`: scope and milestones of the first release (release candidate) and what comes later

The app is a native desktop app in C# (.NET 10 LTS) with Avalonia for Linux, Windows and macOS. Audio runs through the .NET audio engine SoundFlow (chosen by prototype check 2; OwnAudioSharp is the fallback); individual parts such as time-stretching may be written in C/C++ and called via P/Invoke. The project must never cause costs (no paid services, no ads, no donations). Anything new is discussed with the user before it is set up.

## Commands

Requires the .NET 10 SDK (pinned in `global.json`) and, for native code, CMake and a C++ compiler. Developer onboarding is in `README.md` and `CONTRIBUTING.md` (English).

```bash
dotnet run tools/setup.cs                       # check tools, build missing native libs, build solution (-- --check, -- --native)
dotnet build                                    # build everything (warnings are errors)
dotnet test                                     # run all tests (xUnit v3 on Microsoft Testing Platform)
dotnet test --project tests/JustPractise.Core.Tests   # one test project
dotnet run --project src/JustPractise.App       # start the app
dotnet run tools/play.cs -- <file> --check      # decode a file without audio; without --check it plays (user only)
dotnet run tools/startup.cs                     # startup time and package size with/without ReadyToRun (opens the audio device: user only)
dotnet tool restore && dotnet run tools/pack.cs -- --version 0.1.0-rc.1   # Velopack packages into artifacts/releases/<rid>/ (Linux needs mksquashfs)
```

Filtering single tests uses Microsoft Testing Platform syntax after `--`, e.g. `dotnet test --project tests/JustPractise.Core.Tests -- --filter-method "*Grouping*"`.

Empty test projects pass `--ignore-exit-code 8` (no tests found); remove that property from a test project once it contains tests.

## Architecture

- `src/JustPractise.Core`: domain logic without dependencies (grouping files into pieces, models for sidecar files and settings, note and chord names, timer and metronome logic, key bindings)
- `src/JustPractise.Audio`: audio layer behind its own interface (engine, player, metronome clicks, pitch detection, level metering, waveform peaks, native interop). References Core only.
- `src/JustPractise.Storage`: file system and persistence (folder traversal, atomic sidecar/settings I/O, caches, file watching). References Core only.
- `src/JustPractise.App`: Avalonia UI with MVVM (CommunityToolkit.Mvvm), organized by feature folders (`Shell/`, later `FileBrowser/`, `Player/`, `Timer/`, `Metronome/`, `Tuner/`, `Settings/`). `CompositionRoot.cs` is the only place that touches the DI container.
- `tests/`: one xUnit v3 project per library
- `native/`: C/C++ code built with CMake; each folder with a `CMakeLists.txt` builds a shared library `jp<folder>` into `native/bin/<rid>/` (ignored by git)
- `prototypes/`: small standalone test programs for individual prototype checks, not part of the solution
- `site/`: the website on GitHub Pages (plain HTML and CSS, no build step), English at the root and German in `site/de/`; it holds the user installation guide. Both languages are kept in sync. The website and README describe only what already works; planned features are never mentioned there (no "planned", "coming soon", "not yet"), they live only in the concept documents. `.github/workflows/pages.yml` fills in `{{version}}` with the newest release.
- `tools/`: developer scripts as single C# files run with `dotnet run` (file-based apps; `Directory.Build.props` and analyzers apply to them too)

Shared build settings live in `Directory.Build.props` (nullable, warnings as errors, recommended analyzers, code style enforced in build), package versions centrally in `Directory.Packages.props`, code style in `.editorconfig`.

## Conventions

See "Weitere Festlegungen" in `docs/technologie.md`. In short: classes are `sealed` by default, composition over inheritance, interfaces only at real boundaries, immutable records for data, constructor injection (views have parameterless constructors; the composition root sets their `DataContext`), time via `TimeProvider`, no allocations or locks on the audio thread. GC (prototype check 1): workstation GC with background GC (on Linux the segments GC `libclrgc.so`), never `GC.Collect`, decoded audio in native memory blocks (`AudioBlock`), other long-lived data in few large arrays, warm up player and metronome before playback; a blocking full GC stalls the managed audio callback for 40+ ms.
