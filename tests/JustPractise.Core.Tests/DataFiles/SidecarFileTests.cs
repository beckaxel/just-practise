using JustPractise.Core.DataFiles;
using JustPractise.Core.Pieces;

namespace JustPractise.Core.Tests.DataFiles;

public sealed class SidecarFileTests
{
    private const string Example = """
        {
          "format": 1,
          "files": {
            "Autumn Leaves - Demo.mp3": {
              "loop": { "startMs": 12350, "endMs": 47800, "enabled": true },
              "tempo": 0.8,
              "volumeDb": -3.0
            },
            "Autumn Leaves - Playalong 80.mp3": {
              "volumeDb": 2.5
            }
          },
          "members": [
            "Autumn Leaves - Demo.mp3",
            "Autumn Leaves - Playalong 80.mp3",
            "Autumn Leaves.pdf"
          ]
        }
        """;

    [Fact]
    public void ReadsTheExampleFromTheConcept()
    {
        var result = AppDataFiles.Sidecar.Read(Example);

        Assert.Equal(DataFileStatus.Ok, result.Status);
        var demo = result.Value.SettingsFor("Autumn Leaves - Demo.mp3");
        Assert.Equal(0.8, demo.EffectiveTempo);
        Assert.Equal(-3.0, demo.EffectiveVolumeDb);
        Assert.Equal((12350, 47800, true), (demo.Loop?.StartMs, demo.Loop?.EndMs, demo.Loop?.Enabled));
        Assert.Equal(1.0, result.Value.SettingsFor("Autumn Leaves - Playalong 80.mp3").EffectiveTempo);
        Assert.Equal(3, result.Value.Members?.Count);
    }

    [Fact]
    public void UsesDefaultsForUnknownFiles()
    {
        var settings = new SidecarFile().SettingsFor("Blues.mp3");

        Assert.Equal(1.0, settings.EffectiveTempo);
        Assert.Equal(0.0, settings.EffectiveVolumeDb);
        Assert.Null(settings.Loop);
    }

    [Fact]
    public void WritesOnlyDeviations()
    {
        var sidecar = new SidecarFile()
            .WithSettings("Blues.mp3", FileSettings.Default.WithTempo(0.75))
            .WithSettings("Blues - Playalong.mp3", FileSettings.Default.WithVolumeDb(0.0));

        var json = AppDataFiles.Sidecar.Write(sidecar);

        Assert.Equal(
            """
            {
              "format": 1,
              "files": {
                "Blues.mp3": {
                  "tempo": 0.75
                }
              }
            }

            """,
            json);
    }

    [Fact]
    public void RemovesAnEntryThatIsBackToDefault()
    {
        var sidecar = new SidecarFile()
            .WithSettings("Blues.mp3", FileSettings.Default.WithTempo(0.75))
            .WithSettings("Blues.mp3", FileSettings.Default.WithTempo(1.0));

        Assert.Empty(sidecar.Files);
    }

    [Fact]
    public void RoundsAndClampsTheTempo()
    {
        Assert.Equal(0.83, FileSettings.Default.WithTempo(0.8312).Tempo);
        Assert.Equal(1.5, FileSettings.Default.WithTempo(3.0).Tempo);
        Assert.Equal(0.5, new FileSettings { Tempo = 0.1 }.EffectiveTempo);
    }

    [Fact]
    public void KeepsUnknownFieldsOnEveryLevel()
    {
        var json = """
            {
              "format": 1,
              "notes": { "page1": [1, 2, 3] },
              "files": {
                "Blues.mp3": {
                  "tempo": 0.9,
                  "pitchSemitones": -2,
                  "loop": { "startMs": 1, "endMs": 2, "enabled": false, "countIn": true }
                }
              }
            }
            """;

        var sidecar = AppDataFiles.Sidecar.Read(json).Value;
        var changed = sidecar.WithSettings("Blues.mp3", sidecar.SettingsFor("Blues.mp3").WithTempo(0.7));
        var written = AppDataFiles.Sidecar.Write(changed);

        Assert.Contains("\"notes\": {", written, StringComparison.Ordinal);
        Assert.Contains("\"pitchSemitones\": -2", written, StringComparison.Ordinal);
        Assert.Contains("\"countIn\": true", written, StringComparison.Ordinal);
        Assert.Contains("\"tempo\": 0.7", written, StringComparison.Ordinal);
    }

    [Fact]
    public void KeepsAnEntryThatOnlyHasUnknownFields()
    {
        var sidecar = AppDataFiles.Sidecar.Read("""{ "format": 1, "files": { "Blues.mp3": { "tempo": 0.9, "pitchSemitones": -2 } } }""").Value;

        var reset = sidecar.WithSettings("Blues.mp3", sidecar.SettingsFor("Blues.mp3").WithTempo(1.0));

        Assert.Single(reset.Files);
    }

    [Fact]
    public void WritesUmlautsReadably()
    {
        var sidecar = new SidecarFile().WithSettings("Über den Sternen.mp3", FileSettings.Default.WithVolumeDb(-1));

        Assert.Contains("\"Über den Sternen.mp3\"", AppDataFiles.Sidecar.Write(sidecar), StringComparison.Ordinal);
    }

    [Fact]
    public void NamesTheFileAfterThePiece() =>
        Assert.Equal("02 August Song.jps", SidecarFile.FileNameFor("02 August Song"));
}
