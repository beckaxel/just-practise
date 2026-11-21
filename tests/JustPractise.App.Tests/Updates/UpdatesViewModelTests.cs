using System.Globalization;
using System.Net;
using JustPractise.App.Localization;
using JustPractise.App.Updates;

namespace JustPractise.App.Tests.Updates;

public sealed class UpdatesViewModelTests
{
    private readonly FakeUpdateService _service = new();
    private readonly Translator _translator = new(TranslationCatalog.Load(null), "en", CultureInfo.GetCultureInfo("en-US"));

    private UpdatesViewModel Create() => new(_service, _translator);

    [Fact]
    public void WithoutAnInstallationThereIsNothingToCheck()
    {
        _service.InstalledVersion = null;

        var updates = Create();

        Assert.Equal(UpdateState.NotInstalled, updates.State);
        Assert.False(updates.ShowCheck);
        Assert.False(updates.CheckCommand.CanExecute(null));
        Assert.Equal("Updates work only in the installed app.", updates.VersionText);
    }

    [Fact]
    public void ShowsTheInstalledVersionAndChecksOnlyWhenAsked()
    {
        var updates = Create();

        Assert.Equal(UpdateState.Idle, updates.State);
        Assert.Equal("Version 0.1.0", updates.VersionText);
        Assert.Equal("", updates.Message);
        Assert.True(updates.CheckCommand.CanExecute(null));
    }

    [Fact]
    public async Task ReportsThatTheAppIsUpToDate()
    {
        var updates = Create();

        await updates.CheckCommand.ExecuteAsync(null);

        Assert.Equal(UpdateState.UpToDate, updates.State);
        Assert.Equal("just practise is up to date.", updates.Message);
    }

    [Fact]
    public async Task OffersTheNewVersionDownloadsItAndInstallsOnRestart()
    {
        _service.NextCheck = "0.2.0";
        var updates = Create();
        var restartRequested = false;
        updates.RestartRequested += (_, _) => restartRequested = true;

        await updates.CheckCommand.ExecuteAsync(null);
        Assert.Equal(UpdateState.Available, updates.State);
        Assert.Equal("Version 0.2.0 is available.", updates.Message);
        Assert.False(updates.ShowCheck);
        Assert.True(updates.DownloadCommand.CanExecute(null));

        await updates.DownloadCommand.ExecuteAsync(null);
        Assert.Equal(UpdateState.Ready, updates.State);
        Assert.Equal("The update will be installed the next time you start just practise.", updates.Message);
        Assert.False(restartRequested);

        updates.RestartCommand.Execute(null);
        Assert.True(_service.AppliedAfterExit);
        Assert.True(restartRequested);
    }

    [Fact]
    public void AnUpdateDownloadedEarlierIsReadyRightAway()
    {
        _service.IsRestartPending = true;

        var updates = Create();

        Assert.Equal(UpdateState.Ready, updates.State);
        Assert.True(updates.RestartCommand.CanExecute(null));
    }

    [Fact]
    public async Task WithoutAConnectionItSaysSoAndCanTryAgain()
    {
        _service.NextCheck = new HttpRequestException("No such host");
        var updates = Create();

        await updates.CheckCommand.ExecuteAsync(null);

        Assert.Equal(UpdateState.NoConnection, updates.State);
        Assert.Equal("No connection to the internet.", updates.Message);
        Assert.True(updates.CheckCommand.CanExecute(null));
    }

    [Fact]
    public async Task AnswersFromTheServerAndOtherErrorsCountAsFailed()
    {
        _service.NextCheck = new HttpRequestException("Not found", null, HttpStatusCode.NotFound);
        var updates = Create();

        await updates.CheckCommand.ExecuteAsync(null);
        Assert.Equal(UpdateState.Failed, updates.State);

        _service.NextCheck = "0.2.0";
        await updates.CheckCommand.ExecuteAsync(null);
        _service.DownloadFailure = new InvalidOperationException("Checksum mismatch");
        await updates.DownloadCommand.ExecuteAsync(null);
        Assert.Equal(UpdateState.Failed, updates.State);
    }

    [Fact]
    public async Task FollowsTheLanguage()
    {
        var updates = Create();
        await updates.CheckCommand.ExecuteAsync(null);

        _translator.SetLanguage("de");

        Assert.Equal("just practise ist auf dem neuesten Stand.", updates.Message);
        Assert.Equal("Version 0.1.0", updates.VersionText);
    }
}
