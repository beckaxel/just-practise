using System.Globalization;
using JustPractise.App.Localization;
using JustPractise.App.Shell;
using JustPractise.Audio;
using JustPractise.Core.Settings;
using JustPractise.Storage;

namespace JustPractise.App.Tests.Shell;

public sealed class StatusBarViewModelTests : IAsyncDisposable
{
    private readonly FakeDevice _device = new();
    private readonly Translator _translator = new(TranslationCatalog.Load(null), "en", CultureInfo.GetCultureInfo("en-US"));
    private readonly TemporaryDirectory _directory = new();
    private readonly DataFileKeeper<DeviceMemory> _memory;
    private readonly DeveloperMode _developer;

    public StatusBarViewModelTests()
    {
        _memory = new DataFileKeeper<DeviceMemory>(new AppDataLocation(_directory.Path).Device());
        _developer = new DeveloperMode(_memory) { IsEnabled = true };
    }

    public async ValueTask DisposeAsync()
    {
        await _memory.FlushAsync();
        _directory.Dispose();
    }

    [Fact]
    public void ShowsTheDeviceAndHidesLateCallbacksWhileThereAreNone()
    {
        var status = new StatusBarViewModel(_device, _translator, _developer);

        status.Tick();

        Assert.True(status.HasDevice);
        Assert.Equal("Output: Speakers", status.DeviceText);
        Assert.False(status.HasLateCallbacks);
    }

    [Fact]
    public void CountsLateCallbacksInTheChosenLanguage()
    {
        var status = new StatusBarViewModel(_device, _translator, _developer);

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
        var status = new StatusBarViewModel(_device, _translator, _developer);
        _device.IsAvailable = false;
        _device.LateCallbacks = 12; // Ohne Gerät läuft nur der Ersatztakt, dessen Verspätungen zählen nicht.

        status.Tick();

        Assert.False(status.HasDevice);
        Assert.StartsWith("No output device", status.DeviceText, StringComparison.Ordinal);
        Assert.False(status.HasLateCallbacks);
    }

    [Fact]
    public void HidesLateCallbacksOutsideDeveloperMode()
    {
        var status = new StatusBarViewModel(_device, _translator, _developer);
        _device.LateCallbacks = 5;
        _developer.IsEnabled = false;

        status.Tick();
        Assert.False(status.HasLateCallbacks);

        _developer.IsEnabled = true;
        status.Tick();
        Assert.True(status.HasLateCallbacks);
        Assert.Equal("5 late audio callbacks", status.LateText);
    }

    private sealed class FakeDevice : IAudioDevice
    {
        public bool IsAvailable { get; set; } = true;

        public string? Name { get; set; } = "Speakers";

        public long LateCallbacks { get; set; }
    }
}
