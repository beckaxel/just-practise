using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace JustPractise.Audio.Player;

/// <summary>P/Invoke auf <c>native/stretch</c> (<c>jp_stretch.h</c>). Puffer sind interleaved, Längen in Frames.</summary>
internal static partial class StretchNativeMethods
{
    public const int ApiVersion = 3;

    private const string Library = "jpstretch";

    [LibraryImport(Library, EntryPoint = "jp_stretch_api_version")]
    public static partial int GetApiVersion();

    [LibraryImport(Library, EntryPoint = "jp_stretch_create")]
    public static partial StretchHandle Create(int channels, float inputRate, float outputRate, float maxRate, int options);

    [LibraryImport(Library, EntryPoint = "jp_stretch_destroy")]
    public static partial void Destroy(nint stretch);

    [LibraryImport(Library, EntryPoint = "jp_stretch_output_seek_length")]
    public static partial int OutputSeekLength(StretchHandle stretch, double rate);

    [LibraryImport(Library, EntryPoint = "jp_stretch_output_seek")]
    public static partial void OutputSeek(StretchHandle stretch, ReadOnlySpan<float> input, int frames);

    [LibraryImport(Library, EntryPoint = "jp_stretch_process")]
    public static partial void Process(StretchHandle stretch, ReadOnlySpan<float> input, int inputFrames, Span<float> output, int outputFrames);
}

/// <summary>Gibt das native Objekt sicher frei, auch wenn <c>Dispose</c> vergessen wird.</summary>
internal sealed class StretchHandle : SafeHandleZeroOrMinusOneIsInvalid
{
    public StretchHandle()
        : base(ownsHandle: true)
    {
    }

    protected override bool ReleaseHandle()
    {
        StretchNativeMethods.Destroy(handle);
        return true;
    }
}
