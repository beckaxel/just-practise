# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Repository state

The concept phase is complete; the prototype is being built on the `prototyp` branch, directly in the final project structure. The concept documents (in German) are the source of truth for features and technical decisions:

- `docs/funktionen.md`: the feature concept (audio player, practice timer, metronome, tuner, keyboard shortcuts, language and music notation, app settings)
- `docs/technologie.md`: technology decision, libraries, project structure, code conventions, file formats, translations, packaging, signing and the prototype checks

The app is a native desktop app in C# (.NET 10 LTS) with Avalonia for Linux, Windows and macOS. Audio runs through a .NET audio engine (SoundFlow or OwnAudioSharp, chosen by prototype check 2); individual parts such as time-stretching may be written in C/C++ and called via P/Invoke. The project must never cause costs (no paid services, no ads, no donations). Anything new is discussed with the user before it is set up.

## Commands

Requires the .NET 10 SDK (pinned in `global.json`) and, for native code later, CMake.

```bash
dotnet build                                    # build everything (warnings are errors)
dotnet test                                     # run all tests (xUnit v3 on Microsoft Testing Platform)
dotnet test --project tests/JustPractise.Core.Tests   # one test project
dotnet run --project src/JustPractise.App       # start the app
```

Filtering single tests uses Microsoft Testing Platform syntax after `--`, e.g. `dotnet test --project tests/JustPractise.Core.Tests -- --filter-method "*Grouping*"`.

Empty test projects pass `--ignore-exit-code 8` (no tests found); remove that property from a test project once it contains tests.

## Architecture

- `src/JustPractise.Core`: domain logic without dependencies (grouping files into pieces, models for sidecar files and settings, note and chord names, timer and metronome logic, key bindings)
- `src/JustPractise.Audio`: audio layer behind its own interface (engine, player, metronome clicks, pitch detection, level metering, waveform peaks, native interop). References Core only.
- `src/JustPractise.Storage`: file system and persistence (folder traversal, atomic sidecar/settings I/O, caches, file watching). References Core only.
- `src/JustPractise.App`: Avalonia UI with MVVM (CommunityToolkit.Mvvm), organized by feature folders (`Shell/`, later `FileBrowser/`, `Player/`, `Timer/`, `Metronome/`, `Tuner/`, `Settings/`). `CompositionRoot.cs` is the only place that touches the DI container.
- `tests/`: one xUnit v3 project per library
- `native/`: C/C++ code built with CMake (not yet present); `prototypes/`: small standalone test programs for individual prototype checks (not yet present)

Shared build settings live in `Directory.Build.props` (nullable, warnings as errors, recommended analyzers, code style enforced in build), package versions centrally in `Directory.Packages.props`, code style in `.editorconfig`.

## Conventions

See "Weitere Festlegungen" in `docs/technologie.md`. In short: classes are `sealed` by default, composition over inheritance, interfaces only at real boundaries, immutable records for data, constructor injection (views have parameterless constructors; the composition root sets their `DataContext`), time via `TimeProvider`, no allocations or locks on the audio thread.
