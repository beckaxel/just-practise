using System.Globalization;
using JustPractise.App.FileBrowser;
using JustPractise.App.Localization;
using JustPractise.App.Shell;
using JustPractise.App.Tests.Updates;
using JustPractise.App.Updates;
using JustPractise.Core.DataFiles;
using JustPractise.Core.Settings;
using JustPractise.Storage;

using JustPractise.App.Tests.FileBrowser;

namespace JustPractise.App.Tests.Shell;

public sealed class SettingsViewModelTests : IAsyncDisposable
{
    private readonly TemporaryDirectory _directory = new();
    private readonly DataFileKeeper<AppSettings> _settings;
    private readonly DataFileKeeper<DeviceMemory> _memory;
    private readonly Translator _translator = new(TranslationCatalog.Load(null), null, CultureInfo.GetCultureInfo("en-US"));

    public SettingsViewModelTests()
    {
        _settings = new DataFileKeeper<AppSettings>(new DataFileStore<AppSettings>(_directory.File("settings.json"), AppDataFiles.Settings));
        _memory = new DataFileKeeper<DeviceMemory>(new DataFileStore<DeviceMemory>(_directory.File("device.json"), AppDataFiles.Device));
    }

    public async ValueTask DisposeAsync()
    {
        await _settings.FlushAsync();
        await _memory.FlushAsync();
        _directory.Dispose();
    }

    private SettingsViewModel Create() =>
        new(_translator, _settings, new FileBrowserViewModel(_memory, new NoPicker(), new FakeRemoveFolderDialog()), new UpdatesViewModel(new FakeUpdateService(), _translator), new DeveloperMode(_memory));

    [Fact]
    public async Task TurnsTheDeveloperModeOnAndOffAndSavesItForThisComputer()
    {
        var settings = Create();
        Assert.False(settings.Developer.IsEnabled);

        settings.Developer.IsEnabled = true;
        await _memory.FlushAsync();
        Assert.True(new DataFileStore<DeviceMemory>(_directory.File("device.json"), AppDataFiles.Device).Load().Value.Developer.Enabled);

        settings.Developer.IsEnabled = false;
        await _memory.FlushAsync();
        Assert.False(new DataFileStore<DeviceMemory>(_directory.File("device.json"), AppDataFiles.Device).Load().Value.Developer.Enabled);
    }

    [Fact]
    public void OffersTheSystemLanguageFirstAndSelectsItByDefault()
    {
        var settings = Create();

        Assert.True(settings.Languages[0].IsSystem);
        Assert.Contains(settings.Languages, choice => choice.Code == "de");
        Assert.Contains(settings.Languages, choice => choice.Code == "en");
        Assert.True(settings.Language.IsSystem);
    }

    [Fact]
    public async Task SwitchesTheLanguageAtOnceAndSavesIt()
    {
        var settings = Create();

        settings.Language = settings.Languages.Single(choice => choice.Code == "de");
        await _settings.FlushAsync();

        Assert.Equal("de", _translator.Language);
        Assert.Equal("de", new DataFileStore<AppSettings>(_directory.File("settings.json"), AppDataFiles.Settings).Load().Value.Language.Ui);

        settings.Language = settings.Languages[0];
        Assert.Equal("en", _translator.Language); // wie das System (hier englisch)
        Assert.Null(_settings.Value.Language.Ui);
    }

    [Fact]
    public void SelectsTheSavedLanguage()
    {
        _settings.Update(value => value with { Language = new LanguageSettings { Ui = "de" } });

        Assert.Equal("de", Create().Language.Code);
    }

    [Fact]
    public void RequestsClosing()
    {
        var settings = Create();
        var closed = false;
        settings.CloseRequested += (_, _) => closed = true;

        settings.CloseCommand.Execute(null);

        Assert.True(closed);
    }

    private sealed class NoPicker : IFolderPicker
    {
        public Task<string?> PickFolderAsync() => Task.FromResult<string?>(null);
    }
}
