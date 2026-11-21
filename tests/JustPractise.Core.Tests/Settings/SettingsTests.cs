using JustPractise.Core.DataFiles;
using JustPractise.Core.Metronome;
using JustPractise.Core.Settings;

namespace JustPractise.Core.Tests.Settings;

public sealed class SettingsTests
{
    [Fact]
    public void WritesOnlyTheFormatForDefaults()
    {
        Assert.Equal("{\n  \"format\": 1\n}\n", AppDataFiles.Settings.Write(new AppSettings()));
        Assert.Equal("{\n  \"format\": 1\n}\n", AppDataFiles.Device.Write(new DeviceMemory()));
    }

    [Fact]
    public void ReadsTheMetronomeFromTheConceptExample()
    {
        var json = """
            {
              "format": 1,
              "language": { "ui": "de", "noteNames": "german", "accidentals": "sharp" },
              "timer": { "minutes": 25 },
              "metronome": {
                "current": { "bpm": 92, "meter": "7/8", "subdivision": "none", "sound": "woodblock" },
                "presets": [
                  { "name": "Etüde 7/8, 92 BPM", "settings": { "bpm": 92, "meter": "7/8" } }
                ]
              },
              "shortcuts": { "metronome.toggle": "N" }
            }
            """;

        var result = AppDataFiles.Settings.Read(json);

        Assert.Equal(DataFileStatus.Ok, result.Status);
        Assert.Equal("de", result.Value.Language.Ui);
        Assert.Equal(92, result.Value.Metronome.Current.EffectiveBpm);
        Assert.Equal(new Meter(7, 8), result.Value.Metronome.Current.EffectiveMeter);
    }

    [Fact]
    public void KeepsUnknownSettingsWhenTheMetronomeChanges()
    {
        var json = """
            {
              "format": 1,
              "timer": { "minutes": 25 },
              "metronome": {
                "current": { "bpm": 92, "meter": "7/8", "sound": "woodblock" },
                "presets": [ { "name": "Etüde" } ]
              }
            }
            """;
        var settings = AppDataFiles.Settings.Read(json).Value;

        var changed = settings with
        {
            Metronome = settings.Metronome with { Current = settings.Metronome.Current.WithBpm(100) },
        };
        var written = AppDataFiles.Settings.Write(changed);

        Assert.Contains("\"minutes\": 25", written, StringComparison.Ordinal);
        Assert.Contains("\"sound\": \"woodblock\"", written, StringComparison.Ordinal);
        Assert.Contains("\"name\": \"Etüde\"", written, StringComparison.Ordinal);
        Assert.Contains("\"meter\": \"7/8\"", written, StringComparison.Ordinal);
        Assert.Contains("\"bpm\": 100", written, StringComparison.Ordinal);
    }

    [Fact]
    public void StoresOnlyMetronomeDeviations()
    {
        var setup = MetronomeSetup.Default.WithBpm(90).WithMeter(Meter.ThreeFour);
        var settings = new AppSettings { Metronome = MetronomeSettings.Default.WithVolumeDb(-6) with { Current = setup } };

        Assert.Equal(
            """
            {
              "format": 1,
              "metronome": {
                "current": {
                  "bpm": 90,
                  "meter": "3/4"
                },
                "volumeDb": -6
              }
            }

            """,
            AppDataFiles.Settings.Write(settings));

        var reset = setup.WithBpm(MetronomeSetup.DefaultBpm).WithMeter(Meter.FourFour);
        Assert.True(reset.IsDefault);
    }

    [Fact]
    public void FallsBackForInvalidMetronomeValues()
    {
        var setup = new MetronomeSetup { Bpm = 1000, Meter = "schnell" };

        Assert.Equal(MetronomeSetup.MaxBpm, setup.EffectiveBpm);
        Assert.Equal(Meter.FourFour, setup.EffectiveMeter);
    }

    [Fact]
    public void RoundTripsTheDeviceMemory()
    {
        var device = new DeviceMemory { MusicFolders = ["/home/axel/Musik", "/mnt/usb/Noten"], LastFolder = "/home/axel/Musik/LyraGruppe" };

        var read = AppDataFiles.Device.Read(AppDataFiles.Device.Write(device)).Value;

        Assert.Equal(device.MusicFolders, read.MusicFolders);
        Assert.Equal(device.LastFolder, read.LastFolder);
    }
}
