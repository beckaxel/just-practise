using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JustPractise.App.FileBrowser;
using JustPractise.App.Localization;
using JustPractise.App.Updates;
using JustPractise.Core.Settings;
using JustPractise.Storage;

namespace JustPractise.App.Shell;

/// <summary>Eine Sprache zur Auswahl; <see cref="Code"/> <c>null</c> heißt „wie das System“.</summary>
public sealed record LanguageChoice(string? Code, string Name)
{
    public bool IsSystem => Code is null;
}

/// <summary>Das Einstellungsfenster: Sprache der Oberfläche, Musikordner, Entwickler-Modus und Updates.</summary>
/// <remarks>Die Musikordner verwaltet der Dateibrowser; dieses Fenster zeigt sie nur an und reicht Änderungen weiter.</remarks>
public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly Translator _translator;
    private readonly DataFileKeeper<AppSettings> _settings;

    public SettingsViewModel(
        Translator translator,
        DataFileKeeper<AppSettings> settings,
        FileBrowserViewModel browser,
        UpdatesViewModel updates,
        DeveloperMode developer)
    {
        ArgumentNullException.ThrowIfNull(translator);
        ArgumentNullException.ThrowIfNull(settings);
        _translator = translator;
        _settings = settings;
        Browser = browser;
        Updates = updates;
        Developer = developer;
        Languages = [new LanguageChoice(null, ""), .. translator.Languages.Select(option => new LanguageChoice(option.Code, option.Name))];
        var saved = settings.Value.Language.Ui;
        Language = Languages.FirstOrDefault(choice => choice.Code == saved) ?? Languages[0];
    }

    public IReadOnlyList<LanguageChoice> Languages { get; }

    /// <summary>Die gewählte Sprache. Ein Wechsel gilt sofort, ohne Neustart.</summary>
    [ObservableProperty]
    public partial LanguageChoice Language { get; set; }

    /// <summary>Der Browser mit den Musikordnern und den Befehlen zum Hinzufügen und Entfernen.</summary>
    public FileBrowserViewModel Browser { get; }

    public UpdatesViewModel Updates { get; }

    /// <summary>Der Entwickler-Modus zum Ein- und Ausschalten.</summary>
    public DeveloperMode Developer { get; }

    /// <summary>Fordert das Schließen des Fensters an.</summary>
    public event EventHandler? CloseRequested;

    [RelayCommand]
    private void Close() => CloseRequested?.Invoke(this, EventArgs.Empty);

    partial void OnLanguageChanged(LanguageChoice value)
    {
        if (value is null)
        {
            return; // Die Auswahlliste setzt kurz null, wenn sie neu aufgebaut wird.
        }

        _translator.SetLanguage(value.Code);
        _settings.Update(settings => settings with { Language = settings.Language with { Ui = value.Code } });
    }
}
