using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace JustPractise.Prototypes.StretchCheck;

/// <summary>P/Invoke auf native/stretch (jp_stretch.h). Puffer sind interleaved, Längen in Frames.</summary>
internal static partial class NativeMethods
{
    public const int ApiVersion = 1;
    public const int OptionCheaper = 1;
    public const int OptionSplit = 2;

    private const string Library = "jpstretch";

    [LibraryImport(Library, EntryPoint = "jp_stretch_api_version")]
    public static partial int GetApiVersion();

    [LibraryImport(Library, EntryPoint = "jp_stretch_create")]
    public static partial StretchHandle Create(int channels, float sampleRate, int options);

    [LibraryImport(Library, EntryPoint = "jp_stretch_destroy")]
    public static partial void Destroy(nint stretch);

    [LibraryImport(Library, EntryPoint = "jp_stretch_reset")]
    public static partial void Reset(StretchHandle stretch);

    [LibraryImport(Library, EntryPoint = "jp_stretch_input_latency")]
    public static partial int InputLatency(StretchHandle stretch);

    [LibraryImport(Library, EntryPoint = "jp_stretch_output_latency")]
    public static partial int OutputLatency(StretchHandle stretch);

    [LibraryImport(Library, EntryPoint = "jp_stretch_seek_length")]
    public static partial int SeekLength(StretchHandle stretch);

    [LibraryImport(Library, EntryPoint = "jp_stretch_seek")]
    public static partial void Seek(StretchHandle stretch, ReadOnlySpan<float> input, int frames, double rate);

    [LibraryImport(Library, EntryPoint = "jp_stretch_process")]
    public static partial void Process(
        StretchHandle stretch, ReadOnlySpan<float> input, int inputFrames, Span<float> output, int outputFrames);
}

/// <summary>Gibt das native Objekt sicher frei, auch wenn Dispose vergessen wird.</summary>
internal sealed class StretchHandle : SafeHandleZeroOrMinusOneIsInvalid
{
    public StretchHandle()
        : base(ownsHandle: true)
    {
    }

    protected override bool ReleaseHandle()
    {
        NativeMethods.Destroy(handle);
        return true;
    }
}
