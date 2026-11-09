# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Repository state

The project is in the concept phase and has no source code yet. Besides `README.md`, an MIT `LICENSE` and a `.gitignore` from GitHub's `Dotnet.gitignore` template, it contains the concept documents (in German):

- `docs/funktionen.md`: the feature concept (audio player, practice timer, metronome, tuner, keyboard shortcuts, language and music notation, app settings)
- `docs/technologie.md`: the technology decision, its rationale, libraries, data storage and prototype checks

The technology is decided: a native desktop app in C# (.NET LTS) with Avalonia for Linux, Windows and macOS. Audio runs through a .NET audio engine (SoundFlow or OwnAudioSharp, chosen by the prototype); individual parts such as time-stretching may be written in C/C++ and called via P/Invoke. The decision is subject to the prototype checks in `docs/technologie.md`. An earlier PWA decision (TypeScript, Svelte, Vite) was dropped. The `.gitignore` is the .NET template, which fits, but should be reviewed once the project is set up. Anything new is discussed with the user before it is set up.

There are no build, lint, or test commands to document until a project is added. Once one exists (e.g. a `.sln` or `.csproj`), update this file with the actual commands and a short architecture overview.
