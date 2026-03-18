namespace NetImgHash.Internal;

internal static class DifferenceHashAlgorithm
{
    private const int Width = 9;
    private const int Height = 8;
    private const int TotalBits = 64;

    public static ImageHash Compute(Stream stream)
    {
        var pixels = ImageProcessing.LoadResizedGrayscaleBuffer(stream, Width, Height);
        ulong value = 0;
        var bitIndex = 0;

        for (var y = 0; y < Height; y++)
        {
            var rowOffset = y * Width;
            for (var x = 0; x < Width - 1; x++)
            {
                var left = pixels[rowOffset + x];
                var right = pixels[rowOffset + x + 1];

                if (right > left)
                {
                    value |= 1UL << (TotalBits - 1 - bitIndex);
                }

                bitIndex++;
            }
        }

        return new ImageHash(value, TotalBits);
    }
}
