using JustPractise.Audio;

namespace JustPractise.App.Tests.Player;

/// <summary>Ein Player ohne Audio, der Aufrufe mitschreibt. Laden kann man anhalten oder scheitern lassen.</summary>
internal sealed class FakePlayer : IPlayer
{
    private readonly Dictionary<string, TaskCompletionSource> _gates = [];

    public PlayerState State { get; set; }

    public List<string> Calls { get; } = [];

    public double Tempo { get; private set; } = 1.0;

    public double VolumeDb { get; private set; }

    public Exception? LoadFailure { get; set; }

    /// <summary>Laden dieser Datei wartet, bis <see cref="Release"/> aufgerufen wird.</summary>
    public void Hold(string path) => _gates[path] = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

    public void Release(string path) => _gates[path].SetResult();

    public async Task LoadAsync(string path, CancellationToken cancellationToken = default)
    {
        Calls.Add($"load {Path.GetFileName(path)}");
        if (_gates.TryGetValue(path, out var gate))
        {
            await gate.Task;
        }

        if (LoadFailure is { } failure)
        {
            throw failure;
        }

        State = new PlayerState(path, false, TimeSpan.Zero, TimeSpan.FromMinutes(3), true);
    }

    public void Unload() => Calls.Add("unload");

    public void Play()
    {
        Calls.Add("play");
        State = State with { IsPlaying = true };
    }

    public void Pause()
    {
        Calls.Add("pause");
        State = State with { IsPlaying = false };
    }

    public void Seek(TimeSpan position)
    {
        Calls.Add($"seek {position.TotalSeconds}");
        State = State with { Position = position };
    }

    public void ToStart()
    {
        Calls.Add("start");
        State = State with { Position = TimeSpan.Zero };
    }

    public void SetTempo(double tempo) => Tempo = tempo;

    public void SetVolume(double volumeDb) => VolumeDb = volumeDb;
}
