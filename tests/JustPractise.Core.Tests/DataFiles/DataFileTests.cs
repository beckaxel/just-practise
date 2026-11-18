using JustPractise.Core.DataFiles;

namespace JustPractise.Core.Tests.DataFiles;

public sealed class DataFileTests
{
    [Fact]
    public void ReadsAFileFromANewerVersionButForbidsOverwriting()
    {
        var result = AppDataFiles.Sidecar.Read("""{ "format": 2, "files": { "Blues.mp3": { "tempo": 0.9 } } }""");

        Assert.Equal(DataFileStatus.Newer, result.Status);
        Assert.False(result.CanOverwrite);
        Assert.Equal(0.9, result.Value.SettingsFor("Blues.mp3").EffectiveTempo);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("[]")]
    [InlineData("""{ "files": {} }""")]
    [InlineData("""{ "format": "1" }""")]
    [InlineData("""{ "format": 0 }""")]
    [InlineData("""{ "format": 1, "files": { "Blues.mp3": { "tempo": "fast" } } }""")]
    [InlineData("""{ "format": 1, "files": null }""")]
    [InlineData("""{ "format": 1, "files": {""")]
    public void TreatsBrokenFilesAsCorrupt(string json)
    {
        var result = AppDataFiles.Sidecar.Read(json);

        Assert.Equal(DataFileStatus.Corrupt, result.Status);
        Assert.False(result.CanOverwrite);
        Assert.Empty(result.Value.Files);
    }

    [Fact]
    public void ToleratesCommentsAndTrailingCommas()
    {
        var result = AppDataFiles.Sidecar.Read("""
            {
              // von Hand bearbeitet
              "format": 1,
              "files": { "Blues.mp3": { "tempo": 0.9, }, },
            }
            """);

        Assert.Equal(DataFileStatus.Ok, result.Status);
    }

    [Fact]
    public void WritesTheFormatFirst()
    {
        var result = AppDataFiles.Sidecar.Read("""{ "members": ["a.mp3"], "format": 1 }""");

        Assert.StartsWith("{\n  \"format\": 1,", AppDataFiles.Sidecar.Write(result.Value), StringComparison.Ordinal);
    }
}
