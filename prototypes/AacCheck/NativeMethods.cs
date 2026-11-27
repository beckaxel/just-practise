using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace JustPractise.Prototypes.AacCheck;

/// <summary>P/Invoke auf jpaac (jp_aac.h). Puffer sind interleaved, Längen in Frames.</summary>
internal static partial class NativeMethods
{
    public const int ApiVersion = 1;

    private const string Library = "jpaac";

    [LibraryImport(Library, EntryPoint = "jp_aac_api_version")]
    public static partial int GetApiVersion();

    // Die Texte gehören der Bibliothek und dürfen nicht freigegeben werden, daher nint statt string.
    [LibraryImport(Library, EntryPoint = "jp_aac_ffmpeg_version")]
    public static partial nint FfmpegVersion();

    [LibraryImport(Library, EntryPoint = "jp_aac_open", StringMarshalling = StringMarshalling.Utf8)]
    public static partial int Open(string path, out AacHandle handle);

    [LibraryImport(Library, EntryPoint = "jp_aac_close")]
    public static partial void Close(nint aac);

    [LibraryImport(Library, EntryPoint = "jp_aac_channels")]
    public static partial int Channels(AacHandle aac);

    [LibraryImport(Library, EntryPoint = "jp_aac_sample_rate")]
    public static partial int SampleRate(AacHandle aac);

    [LibraryImport(Library, EntryPoint = "jp_aac_length")]
    public static partial long Length(AacHandle aac);

    [LibraryImport(Library, EntryPoint = "jp_aac_codec")]
    public static partial nint Codec(AacHandle aac);

    [LibraryImport(Library, EntryPoint = "jp_aac_profile")]
    public static partial nint Profile(AacHandle aac);

    public static string Text(nint text) => Marshal.PtrToStringUTF8(text) ?? "";

    [LibraryImport(Library, EntryPoint = "jp_aac_read")]
    public static partial int Read(AacHandle aac, Span<float> buffer, int maxFrames);
}

/// <summary>Besitzt ein jp_aac-Objekt.</summary>
internal sealed class AacHandle : SafeHandleZeroOrMinusOneIsInvalid
{
    public AacHandle()
        : base(ownsHandle: true)
    {
    }

    protected override bool ReleaseHandle()
    {
        NativeMethods.Close(handle);
        return true;
    }
}
