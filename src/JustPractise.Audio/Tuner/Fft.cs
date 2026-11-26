namespace JustPractise.Audio.Tuner;

/// <summary>
/// Eine einfache Radix-2-FFT auf getrennten Real- und Imaginärteilen, für die Autokorrelation der Tonhöhenerkennung.
/// Arbeitet in vorher angelegten Puffern und legt beim Rechnen nichts an.
/// </summary>
internal sealed class Fft
{
    private readonly int _size;
    private readonly double[] _cos;
    private readonly double[] _sin;
    private readonly int[] _reversed;

    public Fft(int size)
    {
        if (size < 2 || (size & (size - 1)) != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(size), size, "Die Größe muss eine Zweierpotenz sein.");
        }

        _size = size;
        _cos = new double[size / 2];
        _sin = new double[size / 2];
        for (var i = 0; i < size / 2; i++)
        {
            _cos[i] = Math.Cos(-2 * Math.PI * i / size);
            _sin[i] = Math.Sin(-2 * Math.PI * i / size);
        }

        _reversed = new int[size];
        var bits = (int)Math.Log2(size);
        for (var i = 0; i < size; i++)
        {
            var reversed = 0;
            for (var bit = 0; bit < bits; bit++)
            {
                reversed |= ((i >> bit) & 1) << (bits - 1 - bit);
            }

            _reversed[i] = reversed;
        }
    }

    public int Size => _size;

    /// <summary>Transformiert an Ort und Stelle; <paramref name="inverse"/> ohne Normierung durch die Größe.</summary>
    public void Transform(Span<double> real, Span<double> imaginary, bool inverse)
    {
        for (var i = 0; i < _size; i++)
        {
            var j = _reversed[i];
            if (j > i)
            {
                (real[i], real[j]) = (real[j], real[i]);
                (imaginary[i], imaginary[j]) = (imaginary[j], imaginary[i]);
            }
        }

        var sign = inverse ? -1 : 1;
        for (var length = 2; length <= _size; length <<= 1)
        {
            var half = length / 2;
            var step = _size / length;
            for (var start = 0; start < _size; start += length)
            {
                for (var k = 0; k < half; k++)
                {
                    var cos = _cos[k * step];
                    var sin = sign * _sin[k * step];
                    var a = start + k;
                    var b = a + half;
                    var re = (real[b] * cos) - (imaginary[b] * sin);
                    var im = (real[b] * sin) + (imaginary[b] * cos);
                    real[b] = real[a] - re;
                    imaginary[b] = imaginary[a] - im;
                    real[a] += re;
                    imaginary[a] += im;
                }
            }
        }
    }
}
