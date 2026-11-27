using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JustPractise.App.FileBrowser;
using JustPractise.Audio;
using JustPractise.Core.Pieces;
using JustPractise.Core.Waveform;
using JustPractise.Storage;

namespace JustPractise.App.Player;

/// <summary>
/// Der Player: lädt die im Browser gewählte Datei, spielt sie ab und merkt sich Tempo und Lautstärke pro Datei in der
/// Sidecar-Datei des Stücks. Dazu die Wellenform mit ihrem sichtbaren Ausschnitt (Zoom).
/// </summary>
/// <remarks>
/// Aufrufe kommen vom UI-Thread. Den Stand des Audio-Threads holt <see cref="Tick"/>, das die Oberfläche regelmäßig
/// aufruft. Wählt man schneller neue Dateien, als sie laden, zählt nur die zuletzt gewählte.
/// Der Ausschnitt folgt der Abspielposition seitenweise. Verschiebt man ihn selbst so, dass sie nicht mehr zu sehen
/// ist, folgt er erst wieder, wenn sie ihn erreicht oder man selbst springt.
/// </remarks>
public sealed partial class PlayerViewModel(IPlayer player) : ObservableObject
{
    public const double MinTempoPercent = FileSettings.MinTempo * 100;
    public const double MaxTempoPercent = FileSettings.MaxTempo * 100;
    public const double MinVolumeDb = -20;
    public const double MaxVolumeDb = 10;

    /// <summary>Sprung der Pfeiltasten.</summary>
    public const double SeekSeconds = 5;

    /// <summary>Zoom pro Tipp auf − oder + bzw. pro Tastendruck.</summary>
    public const double ZoomStep = 2;

    private int _version;
    private double _positionSeconds;
    private DataFileKeeper<SidecarFile>? _sidecar;
    private WaveformViewport _viewport;
    private bool _following = true;

    /// <summary>Die gewählte Datei, auch während sie lädt oder wenn sie sich nicht öffnen ließ.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasFile))]
    public partial PieceFile? File { get; private set; }

    public bool HasFile => File is not null;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsReady))]
    public partial bool IsLoading { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsReady))]
    public partial bool HasError { get; private set; }

    /// <summary>Die Datei ist geladen und lässt sich bedienen.</summary>
    public bool IsReady => File is not null && !IsLoading && !HasError;

    [ObservableProperty]
    public partial bool IsPlaying { get; private set; }

    [ObservableProperty]
    public partial double DurationSeconds { get; private set; }

    [ObservableProperty]
    public partial string PositionText { get; private set; } = FormatTime(TimeSpan.Zero);

    [ObservableProperty]
    public partial string DurationText { get; private set; } = FormatTime(TimeSpan.Zero);

    /// <summary>Die Stelle in Sekunden. Setzt die Oberfläche sie (Klick oder Ziehen im Fortschrittsbalken), springt der Player.</summary>
    public double PositionSeconds
    {
        get => _positionSeconds;
        set
        {
            if (IsReady && SetProperty(ref _positionSeconds, value))
            {
                player.Seek(TimeSpan.FromSeconds(value));
                PositionText = FormatTime(TimeSpan.FromSeconds(value));
            }
        }
    }

    /// <summary>Die Spitzen der Wellenform, <c>null</c>, solange sie noch berechnet werden.</summary>
    [ObservableProperty]
    public partial WaveformPeaks? Waveform { get; private set; }

    /// <summary>
    /// Der sichtbare Ausschnitt der Wellenform. Setzt die Oberfläche ihn (Ziehen, Zoomen, Tipp in die Übersicht), folgt er
    /// der Abspielposition nur, solange sie darin zu sehen ist.
    /// </summary>
    public WaveformViewport Viewport
    {
        get => _viewport;
        set
        {
            var viewport = WaveformViewport.Of(DurationSeconds, value.Start, value.Length);
            _following = viewport.Contains(_positionSeconds);
            ShowViewport(viewport);
        }
    }

    /// <summary>Tempo in Prozent, 50 bis 150.</summary>
    [ObservableProperty]
    public partial double TempoPercent { get; set; } = 100;

    /// <summary>Lautstärke der Datei in dB, 0 heißt unverändert.</summary>
    [ObservableProperty]
    public partial double VolumeDb { get; set; }

    /// <summary>Lädt eine Datei, angehalten am Anfang, mit Tempo und Lautstärke aus der Sidecar-Datei.</summary>
    public async Task OpenAsync(PieceFile file)
    {
        ArgumentNullException.ThrowIfNull(file);
        var version = ++_version;
        _sidecar = null; // Änderungen während des Ladens gehören zu keiner Datei.
        File = file;
        IsLoading = true;
        HasError = false;
        IsPlaying = false;
        ShowPosition(TimeSpan.Zero, TimeSpan.Zero);
        ResetWaveform();

        try
        {
            var sidecar = await Task.Run(() => new DataFileKeeper<SidecarFile>(AppDataLocation.Sidecar(file.Folder, file.PieceTitle)));
            await player.LoadAsync(file.Path);
            if (version != _version)
            {
                return;
            }

            // Erst die Werte setzen (das stellt den Player ein), dann die Sidecar-Datei anhängen, damit Laden nicht schreibt.
            var settings = sidecar.Value.SettingsFor(file.FileName);
            TempoPercent = Math.Round(settings.EffectiveTempo * 100);
            VolumeDb = Math.Clamp(settings.EffectiveVolumeDb, MinVolumeDb, MaxVolumeDb);
            player.SetTempo(TempoPercent / 100);
            player.SetVolume(VolumeDb);
            _sidecar = sidecar;
            IsLoading = false;
            Tick();
        }
        catch (OperationCanceledException)
        {
            // Eine andere Datei wurde gewählt.
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException or InvalidOperationException)
        {
            if (version == _version)
            {
                IsLoading = false;
                HasError = true;
            }
        }
    }

    /// <summary>
    /// Schließt die Datei, z. B. wenn ihr Musikordner entfernt wird: hält an, entlädt sie und wartet, bis ausstehende
    /// Änderungen an der Sidecar-Datei geschrieben sind. Danach schreibt der Player nichts mehr dorthin.
    /// </summary>
    public async Task CloseAsync()
    {
        ++_version;
        var sidecar = _sidecar;
        _sidecar = null;
        player.Unload();
        File = null;
        IsLoading = false;
        HasError = false;
        IsPlaying = false;
        ShowPosition(TimeSpan.Zero, TimeSpan.Zero);
        ResetWaveform();
        if (sidecar is not null)
        {
            await sidecar.FlushAsync();
        }
    }

    /// <summary>Wartet, bis Änderungen an der Sidecar-Datei geschrieben sind, z. B. beim Beenden.</summary>
    public Task FlushAsync() => _sidecar?.FlushAsync() ?? Task.CompletedTask;

    /// <summary>Holt den Stand vom Audio-Thread; die Oberfläche ruft das etwa 30-mal pro Sekunde auf.</summary>
    public void Tick()
    {
        if (!IsReady)
        {
            return;
        }

        var state = player.State;
        IsPlaying = state.IsPlaying;
        ShowPosition(state.Position, state.Duration);
        Waveform = state.Waveform;
        Follow();
    }

    [RelayCommand]
    public void PlayPause()
    {
        if (!IsReady)
        {
            return;
        }

        if (IsPlaying)
        {
            player.Pause();
            IsPlaying = false;
            return;
        }

        if (player.State.IsAtEnd)
        {
            player.ToStart(); // Am Ende angekommen: Abspielen fängt von vorn an.
        }

        player.Play();
        IsPlaying = true;
    }

    [RelayCommand]
    public void ToStart()
    {
        if (IsReady)
        {
            player.ToStart();
            ShowPosition(TimeSpan.Zero, TimeSpan.FromSeconds(DurationSeconds));
            _following = true;
            Follow();
        }
    }

    /// <summary>Springt um <paramref name="seconds"/> vor oder zurück, höchstens bis an Anfang oder Ende.</summary>
    public void SeekBy(double seconds)
    {
        if (IsReady)
        {
            PositionSeconds = Math.Clamp(PositionSeconds + seconds, 0, DurationSeconds);
            _following = true;
            Follow();
        }
    }

    /// <summary>Zoomt hinein, um die Abspielposition.</summary>
    [RelayCommand]
    public void ZoomIn() => ZoomAtPosition(ZoomStep);

    /// <summary>Zoomt heraus, um die Abspielposition.</summary>
    [RelayCommand]
    public void ZoomOut() => ZoomAtPosition(1 / ZoomStep);

    /// <summary>Zeigt das ganze Stück.</summary>
    [RelayCommand]
    public void ZoomAll()
    {
        if (IsReady)
        {
            _following = true;
            ShowViewport(WaveformViewport.Whole(DurationSeconds));
        }
    }

    /// <summary>Ändert das Tempo um <paramref name="percent"/> Prozentpunkte, innerhalb von 50 bis 150 %.</summary>
    public void ChangeTempo(double percent)
    {
        if (IsReady)
        {
            TempoPercent = Math.Clamp(TempoPercent + percent, MinTempoPercent, MaxTempoPercent);
        }
    }

    /// <summary>Ändert die Lautstärke um <paramref name="decibels"/>, innerhalb von −20 bis +10 dB.</summary>
    public void ChangeVolume(double decibels)
    {
        if (IsReady)
        {
            VolumeDb = Math.Clamp(VolumeDb + decibels, MinVolumeDb, MaxVolumeDb);
        }
    }

    [RelayCommand]
    private void ResetTempo() => TempoPercent = 100;

    [RelayCommand]
    private void ResetVolume() => VolumeDb = 0;

    partial void OnTempoPercentChanged(double value)
    {
        if (_sidecar is not null)
        {
            player.SetTempo(value / 100);
            SaveSettings(settings => settings.WithTempo(value / 100));
        }
    }

    partial void OnVolumeDbChanged(double value)
    {
        if (_sidecar is not null)
        {
            player.SetVolume(value);
            SaveSettings(settings => settings.WithVolumeDb(value));
        }
    }

    private void ZoomAtPosition(double factor)
    {
        if (IsReady)
        {
            _following = true;
            ShowViewport(_viewport.ZoomAtPosition(factor, _positionSeconds));
        }
    }

    /// <summary>Passt den Ausschnitt an die Länge an und blättert, wenn er der Position folgt und sie hinausgelaufen ist.</summary>
    private void Follow()
    {
        var viewport = _viewport.WithDuration(DurationSeconds);
        if (_following)
        {
            viewport = viewport.PageTo(_positionSeconds);
        }
        else if (viewport.Contains(_positionSeconds))
        {
            _following = true;
        }

        ShowViewport(viewport);
    }

    private void ResetWaveform()
    {
        Waveform = null;
        _following = true;
        ShowViewport(WaveformViewport.Whole(0));
    }

    private void ShowViewport(WaveformViewport viewport) => SetProperty(ref _viewport, viewport, nameof(Viewport));

    private void SaveSettings(Func<FileSettings, FileSettings> change)
    {
        if (_sidecar is { } sidecar && File is { } file)
        {
            sidecar.Update(value => value.WithSettings(file.FileName, change(value.SettingsFor(file.FileName))));
        }
    }

    private void ShowPosition(TimeSpan position, TimeSpan duration)
    {
        DurationSeconds = duration.TotalSeconds;
        DurationText = FormatTime(duration);
        PositionText = FormatTime(position);
        _ = SetProperty(ref _positionSeconds, position.TotalSeconds, nameof(PositionSeconds)); // am Setter vorbei: nur anzeigen, nicht springen
    }

    /// <summary><c>m:ss</c>, ab einer Stunde <c>h:mm:ss</c>.</summary>
    public static string FormatTime(TimeSpan time) =>
        time.TotalHours >= 1
            ? time.ToString(@"h\:mm\:ss", CultureInfo.InvariantCulture)
            : time.ToString(@"m\:ss", CultureInfo.InvariantCulture);
}
