namespace NetImgHash.Internal;

/// <summary>
/// Pillow's <c>Image.resize(..., Image.LANCZOS)</c> for 8-bit grayscale, reproduced
/// operation for operation from <c>libImaging/Resample.c</c>.
/// </summary>
/// <remarks>
/// <para>
/// Every hash in this library is defined by the Python <c>imagehash</c> package, and
/// every one of them thresholds pixels that came out of Pillow's resampler. A resampler
/// that is merely "also Lanczos-3" is not enough: ImageSharp's differs from Pillow's on
/// roughly one pixel in ten by ±1, which is invisible to the eye and enough to flip any
/// bit whose pixel or DCT coefficient sits close to the threshold.
/// </para>
/// <para>
/// The details that matter, all taken from Pillow: coefficients are computed in double,
/// normalised to sum to 1, then rounded to 22-bit fixed point; each pass accumulates in
/// integers starting from a half-unit rounding bias and clips to a byte; the horizontal
/// pass runs first over only the source rows the vertical pass will read, and its 8-bit
/// output is the vertical pass's input, so intermediate rounding happens exactly once,
/// where Pillow does it.
/// </para>
/// </remarks>
internal static class PillowResampler
{
    /// <summary>8 bits for the result and 2 for overflow, leaving 22 for the fraction.</summary>
    private const int PrecisionBits = 32 - 8 - 2;

    private const double LanczosSupport = 3.0;

    public static byte[] ResizeLanczos(ReadOnlySpan<byte> source, int sourceWidth, int sourceHeight, int targetWidth, int targetHeight)
    {
        var needHorizontal = targetWidth != sourceWidth;
        var needVertical = targetHeight != sourceHeight;

        if (!needHorizontal && !needVertical)
        {
            return source.ToArray();
        }

        var horizontal = PrecomputeCoefficients(sourceWidth, targetWidth);
        var vertical = PrecomputeCoefficients(sourceHeight, targetHeight);

        // The vertical pass only reads source rows [firstRow, lastRow), so the horizontal
        // pass only produces those.
        var firstRow = vertical.Bounds[0];
        var lastRow = vertical.Bounds[((targetHeight - 1) * 2) + 0] + vertical.Bounds[((targetHeight - 1) * 2) + 1];

        ReadOnlySpan<byte> current = source;
        var currentWidth = sourceWidth;

        if (needHorizontal)
        {
            for (var i = 0; i < targetHeight; i++)
            {
                vertical.Bounds[i * 2] -= firstRow;
            }

            var rows = lastRow - firstRow;
            var temp = new byte[targetWidth * rows];
            ResampleHorizontal(temp, targetWidth, rows, source, sourceWidth, firstRow, horizontal);
            current = temp;
            currentWidth = targetWidth;
        }

        if (needVertical)
        {
            var output = new byte[currentWidth * targetHeight];
            ResampleVertical(output, currentWidth, targetHeight, current, vertical);
            return output;
        }

        return current.ToArray();
    }

    private static Coefficients PrecomputeCoefficients(int inSize, int outSize)
    {
        var scale = (double)inSize / outSize;
        var filterScale = Math.Max(scale, 1.0);
        var support = LanczosSupport * filterScale;
        var kernelSize = ((int)Math.Ceiling(support) * 2) + 1;

        var weights = new double[outSize * kernelSize];
        var bounds = new int[outSize * 2];

        for (var xx = 0; xx < outSize; xx++)
        {
            var center = (xx + 0.5) * scale;
            var ss = 1.0 / filterScale;

            // (int) truncates toward zero, as the C cast does.
            var xmin = (int)(center - support + 0.5);
            if (xmin < 0)
            {
                xmin = 0;
            }

            var xmax = (int)(center + support + 0.5);
            if (xmax > inSize)
            {
                xmax = inSize;
            }

            xmax -= xmin;

            var k = xx * kernelSize;
            double sum = 0.0;
            for (var x = 0; x < xmax; x++)
            {
                var w = Lanczos((x + xmin - center + 0.5) * ss);
                weights[k + x] = w;
                sum += w;
            }

            if (sum != 0.0)
            {
                for (var x = 0; x < xmax; x++)
                {
                    weights[k + x] /= sum;
                }
            }

            bounds[(xx * 2) + 0] = xmin;
            bounds[(xx * 2) + 1] = xmax;
        }

        // normalize_coeffs_8bpc: to fixed point, rounding half away from zero.
        var fixedWeights = new int[weights.Length];
        for (var i = 0; i < weights.Length; i++)
        {
            var scaled = weights[i] * (1 << PrecisionBits);
            fixedWeights[i] = weights[i] < 0 ? (int)(-0.5 + scaled) : (int)(0.5 + scaled);
        }

        return new Coefficients(kernelSize, bounds, fixedWeights);
    }

    private static void ResampleHorizontal(
        Span<byte> output, int outWidth, int rows,
        ReadOnlySpan<byte> input, int inWidth, int rowOffset,
        Coefficients c)
    {
        for (var yy = 0; yy < rows; yy++)
        {
            var inRow = (yy + rowOffset) * inWidth;
            var outRow = yy * outWidth;
            for (var xx = 0; xx < outWidth; xx++)
            {
                var xmin = c.Bounds[(xx * 2) + 0];
                var xmax = c.Bounds[(xx * 2) + 1];
                var k = xx * c.KernelSize;

                var acc = 1 << (PrecisionBits - 1);
                for (var x = 0; x < xmax; x++)
                {
                    acc += input[inRow + x + xmin] * c.Weights[k + x];
                }

                output[outRow + xx] = Clip8(acc);
            }
        }
    }

    private static void ResampleVertical(
        Span<byte> output, int width, int outHeight,
        ReadOnlySpan<byte> input,
        Coefficients c)
    {
        for (var yy = 0; yy < outHeight; yy++)
        {
            var ymin = c.Bounds[(yy * 2) + 0];
            var ymax = c.Bounds[(yy * 2) + 1];
            var k = yy * c.KernelSize;
            var outRow = yy * width;

            for (var xx = 0; xx < width; xx++)
            {
                var acc = 1 << (PrecisionBits - 1);
                for (var y = 0; y < ymax; y++)
                {
                    acc += input[((y + ymin) * width) + xx] * c.Weights[k + y];
                }

                output[outRow + xx] = Clip8(acc);
            }
        }
    }

    /// <summary>Pillow's <c>clip8</c>: drop the fraction, then clamp to a byte.</summary>
    private static byte Clip8(int value)
    {
        var shifted = value >> PrecisionBits;
        return (byte)Math.Clamp(shifted, 0, 255);
    }

    private static double Lanczos(double x)
    {
        if (x is >= -3.0 and < 3.0)
        {
            return Sinc(x) * Sinc(x / 3);
        }

        return 0.0;
    }

    private static double Sinc(double x)
    {
        if (x == 0.0)
        {
            return 1.0;
        }

        x *= Math.PI;
        return Math.Sin(x) / x;
    }

    private sealed record Coefficients(int KernelSize, int[] Bounds, int[] Weights);
}
