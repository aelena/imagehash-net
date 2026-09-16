namespace NetImgHash.Internal;

/// <summary>
/// DCT-based perceptual hash (pHash), bit-compatible with Python <c>imagehash.phash</c>
/// at its defaults (<c>hash_size=8</c>, <c>highfreq_factor=4</c>).
/// </summary>
/// <remarks>
/// The reference is
/// <c>dct = scipy.fftpack.dct(scipy.fftpack.dct(pixels, axis=0), axis=1)</c>,
/// an un-normalised DCT-II applied along each axis, followed by taking the top-left
/// 8×8 block, its median, and the comparison <c>coefficient &gt; median</c>. Everything
/// below mirrors those choices exactly, including numpy's median for an even count
/// (the mean of the two middle values) and the strict comparison.
/// </remarks>
internal static class PerceptualHashAlgorithm
{
    private const int HashSize = 8;
    private const int HighFrequencyFactor = 4;
    private const int ImageSize = HashSize * HighFrequencyFactor;
    private const int TotalBits = HashSize * HashSize;

    /// <summary>
    /// cos(π·k·(2n+1) / (2N)) for the N×N un-normalised DCT-II, indexed [k * N + n].
    /// Only the first <see cref="HashSize"/> output frequencies are ever read, but the
    /// full table costs 8 KB once and keeps the indexing uniform.
    /// </summary>
    private static readonly double[] CosineTable = BuildCosineTable(ImageSize);

    public static ImageHash Compute(Stream stream)
    {
        var pixels = ImageProcessing.LoadResizedGrayscaleBuffer(stream, ImageSize, ImageSize);

        // scipy.fftpack.dct(pixels, axis=0): a 1-D DCT down each column, keeping only
        // the low HashSize frequencies, since the second pass and the final block
        // never touch the rest. Layout: [frequency row v][column x].
        Span<double> columnPass = stackalloc double[HashSize * ImageSize];
        for (var x = 0; x < ImageSize; x++)
        {
            for (var v = 0; v < HashSize; v++)
            {
                double sum = 0;
                var cosRow = v * ImageSize;
                for (var y = 0; y < ImageSize; y++)
                {
                    sum += pixels[(y * ImageSize) + x] * CosineTable[cosRow + y];
                }

                columnPass[(v * ImageSize) + x] = 2 * sum;
            }
        }

        // scipy.fftpack.dct(..., axis=1): a 1-D DCT along each of those rows, again
        // keeping the low HashSize frequencies. This is the 8×8 block dct[:8, :8].
        Span<double> block = stackalloc double[TotalBits];
        for (var v = 0; v < HashSize; v++)
        {
            var rowOffset = v * ImageSize;
            for (var u = 0; u < HashSize; u++)
            {
                double sum = 0;
                var cosRow = u * ImageSize;
                for (var x = 0; x < ImageSize; x++)
                {
                    sum += columnPass[rowOffset + x] * CosineTable[cosRow + x];
                }

                block[(v * HashSize) + u] = 2 * sum;
            }
        }

        // Mathematically equal coefficients (every AC term of a flat image, or the
        // symmetric pairs of a symmetric one) come out of a direct cosine summation as
        // distinct values a few 1e-10 apart, while scipy's FFT-based transform produces
        // them exactly equal. Rounding restores the equalities so the median and the
        // strict comparison behave as they do in the reference.
        for (var index = 0; index < TotalBits; index++)
        {
            block[index] = Math.Round(block[index], 6);
        }

        var median = Median(block);

        ulong value = 0;
        for (var index = 0; index < TotalBits; index++)
        {
            if (block[index] > median)
            {
                value |= 1UL << (TotalBits - 1 - index);
            }
        }

        return new ImageHash(value, TotalBits);
    }

    /// <summary>
    /// numpy.median for an even-length array: the mean of the two middle values.
    /// </summary>
    private static double Median(ReadOnlySpan<double> values)
    {
        Span<double> sorted = stackalloc double[values.Length];
        values.CopyTo(sorted);
        sorted.Sort();

        var upper = sorted.Length / 2;
        return (sorted[upper - 1] + sorted[upper]) / 2;
    }

    private static double[] BuildCosineTable(int size)
    {
        var table = new double[size * size];
        for (var k = 0; k < size; k++)
        {
            for (var n = 0; n < size; n++)
            {
                table[(k * size) + n] = Math.Cos(Math.PI * k * ((2 * n) + 1) / (2.0 * size));
            }
        }

        return table;
    }
}
