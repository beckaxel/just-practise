using System.Globalization;
using JustPractise.App.Localization;
using JustPractise.App.Shell;
using JustPractise.Audio;

namespace JustPractise.App.Tests.Shell;

public sealed class StatusBarViewModelTests
{
    private readonly FakeDevice _device = new();
    private readonly Translator _translator = new(TranslationCatalog.Load(null), "en", CultureInfo.GetCultureInfo("en-US"));

    [Fact]
    public void ShowsTheDeviceAndHidesLateCallbacksWhileThereAreNone()
    {
        var status = new StatusBarViewModel(_device, _translator);

        status.Tick();

        Assert.True(status.HasDevice);
        Assert.Equal("Output: Speakers", status.DeviceText);
        Assert.False(status.HasLateCallbacks);
    }

    [Fact]
    public void CountsLateCallbacksInTheChosenLanguage()
    {
        var status = new StatusBarViewModel(_device, _translator);

        _device.LateCallbacks = 1;
        status.Tick();
        Assert.Equal("1 late audio callback", status.LateText);

        _device.LateCallbacks = 3;
        _translator.SetLanguage("de");
        status.Tick();
        Assert.True(status.HasLateCallbacks);
        Assert.Equal("3 verspätete Audio-Callbacks", status.LateText);
        Assert.Equal("Ausgabe: Speakers", status.DeviceText);
    }

    [Fact]
    public void WarnsWithoutAnOutputDevice()
    {
        var status = new StatusBarViewModel(_device, _translator);
        _device.IsAvailable = false;
        _device.LateCallbacks = 12; // Ohne Gerät läuft nur der Ersatztakt, dessen Verspätungen zählen nicht.

        status.Tick();

        Assert.False(status.HasDevice);
        Assert.StartsWith("No output device", status.DeviceText, StringComparison.Ordinal);
        Assert.False(status.HasLateCallbacks);
    }

    private sealed class FakeDevice : IAudioDevice
    {
        public bool IsAvailable { get; set; } = true;

        public string? Name { get; set; } = "Speakers";

        public long LateCallbacks { get; set; }
    }
}
