# just practise

A desktop app that helps musicians practise with their own recordings: browse your music folders, play tracks at 50–150 % tempo without changing the pitch, keep time with a metronome, tune your instrument and time your practice. Free and open source, offline, no account, no ads.

**Website:** https://beckaxel.github.io/just-practise/

## Install

Download the installer or the portable version for Windows, or the AppImage for Linux, from the [website](https://beckaxel.github.io/just-practise/) or the [releases](https://github.com/beckaxel/just-practise/releases). The [installation guide](https://beckaxel.github.io/just-practise/install.html) explains each step, the SmartScreen warning on Windows, updates and where your data is.

Release candidates of version 0.1.0 are out. just practise is a native desktop app in C# (.NET 10) with [Avalonia](https://avaloniaui.net/).

## Build from source

You need the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) and Git. Then:

```bash
git clone https://github.com/beckaxel/just-practise.git
cd just-practise
dotnet run tools/setup.cs
dotnet run --project src/JustPractise.App
```

The setup script checks your tools, tells you how to install anything that is missing, and builds the project. Details, including per-OS instructions, are in [CONTRIBUTING.md](CONTRIBUTING.md).

## Documentation

The concept documents are written in German:

- [docs/funktionen.md](docs/funktionen.md): features
- [docs/technologie.md](docs/technologie.md): technology, project structure, conventions and file formats

## License

[MIT](LICENSE)
