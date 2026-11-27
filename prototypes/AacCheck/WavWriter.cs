using System.Buffers.Binary;
using System.Runtime.InteropServices;

namespace JustPractise.Prototypes.AacCheck;

/// <summary>Schreibt WAV mit 32-Bit-float (Formatcode 3), zum Anhören der decodierten Dateien.</summary>
internal sealed class WavWriter : IDisposable
{
    private const int HeaderSize = 44;

    private readonly FileStream stream;
    private readonly int channels;
    private readonly int sampleRate;
    private long dataBytes;

    public WavWriter(string path, int channels, int sampleRate)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        stream = File.Create(path);
        this.channels = channels;
        this.sampleRate = sampleRate;
        stream.Write(new byte[HeaderSize]);
    }

    public void Write(ReadOnlySpan<float> samples)
    {
        stream.Write(MemoryMarshal.AsBytes(samples));
        dataBytes += samples.Length * sizeof(float);
    }

    public void Dispose()
    {
        Span<byte> header = stackalloc byte[HeaderSize];
        "RIFF"u8.CopyTo(header);
        BinaryPrimitives.WriteUInt32LittleEndian(header[4..], (uint)(HeaderSize - 8 + dataBytes));
        "WAVEfmt "u8.CopyTo(header[8..]);
        BinaryPrimitives.WriteUInt32LittleEndian(header[16..], 16);
        BinaryPrimitives.WriteUInt16LittleEndian(header[20..], 3);
        BinaryPrimitives.WriteUInt16LittleEndian(header[22..], (ushort)channels);
        BinaryPrimitives.WriteUInt32LittleEndian(header[24..], (uint)sampleRate);
        BinaryPrimitives.WriteUInt32LittleEndian(header[28..], (uint)(sampleRate * channels * sizeof(float)));
        BinaryPrimitives.WriteUInt16LittleEndian(header[32..], (ushort)(channels * sizeof(float)));
        BinaryPrimitives.WriteUInt16LittleEndian(header[34..], 32);
        "data"u8.CopyTo(header[36..]);
        BinaryPrimitives.WriteUInt32LittleEndian(header[40..], (uint)dataBytes);
        stream.Position = 0;
        stream.Write(header);
        stream.Dispose();
    }
}
