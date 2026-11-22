# just practise

A desktop app that helps musicians practise: an audio player with loops and tempo control, a practice timer, a metronome and a tuner. Free and open source, offline, no account, no ads.

The project is in an early stage: the first release candidate is out (see Releases). It is a native desktop app in C# (.NET 10) with [Avalonia](https://avaloniaui.net/) for Linux, Windows and macOS.

## Quick start

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
