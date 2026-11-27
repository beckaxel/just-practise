using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace JustPractise.Audio.Player;

/// <summary>P/Invoke auf <c>native/aac</c> (<c>jp_aac.h</c>). Puffer sind interleaved, Längen in Frames.</summary>
internal static partial class AacNativeMethods
{
    public const int ApiVersion = 1;

    private const string Library = "jpaac";

    [LibraryImport(Library, EntryPoint = "jp_aac_api_version")]
    public static partial int GetApiVersion();

    [LibraryImport(Library, EntryPoint = "jp_aac_open", StringMarshalling = StringMarshalling.Utf8)]
    public static partial int Open(string path, out AacHandle aac);

    [LibraryImport(Library, EntryPoint = "jp_aac_close")]
    public static partial void Close(nint aac);

    [LibraryImport(Library, EntryPoint = "jp_aac_channels")]
    public static partial int Channels(AacHandle aac);

    [LibraryImport(Library, EntryPoint = "jp_aac_sample_rate")]
    public static partial int SampleRate(AacHandle aac);

    [LibraryImport(Library, EntryPoint = "jp_aac_read")]
    public static partial int Read(AacHandle aac, Span<float> buffer, int maxFrames);
}

/// <summary>Gibt das native Objekt sicher frei, auch wenn <c>Dispose</c> vergessen wird.</summary>
internal sealed class AacHandle : SafeHandleZeroOrMinusOneIsInvalid
{
    public AacHandle()
        : base(ownsHandle: true)
    {
    }

    protected override bool ReleaseHandle()
    {
        AacNativeMethods.Close(handle);
        return true;
    }
}
