using System.Runtime.InteropServices;

namespace JustPractise.Audio.Player;

/// <summary>
/// Ein Block decodiertes Audio im nativen Speicher. Er liegt außerhalb des GC-Heaps, damit das Laden einer Datei
/// keine Speicherbereinigung auslöst und der Heap klein bleibt.
/// </summary>
/// <remarks>
/// Der Inhalt ist nach dem Anlegen nicht gelöscht. Wird <c>Dispose</c> vergessen, gibt der Finalizer den Speicher frei.
/// Lesen nur, solange der Block nicht freigegeben ist; das regeln <see cref="BlockPool"/> und <see cref="DecodedAudio"/>.
/// </remarks>
public sealed class AudioBlock : SafeHandle
{
    public unsafe AudioBlock(int length)
        : base(0, ownsHandle: true)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(length);
        Length = length;
        SetHandle((nint)NativeMemory.Alloc((nuint)length, sizeof(float)));
    }

    /// <summary>Länge in Samples.</summary>
    public int Length { get; }

    public override bool IsInvalid => handle == 0;

    /// <summary>Ein Ausschnitt des Blocks. Läuft auch auf dem Audio-Thread: keine Allokation.</summary>
    public unsafe Span<float> AsSpan(int start, int length) => new Span<float>((void*)handle, Length).Slice(start, length);

    protected override unsafe bool ReleaseHandle()
    {
        NativeMemory.Free((void*)handle);
        return true;
    }
}
