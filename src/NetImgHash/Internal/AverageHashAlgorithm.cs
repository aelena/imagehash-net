namespace NetImgHash.Internal;

internal static class AverageHashAlgorithm
{
    private const int HashSize = 8;
    private const int TotalBits = HashSize * HashSize;

    public static ImageHash Compute(Stream stream)
    {
        var pixels = ImageProcessing.LoadResizedGrayscaleBuffer(stream, HashSize, HashSize);

        long sum = 0;
        foreach (var pixel in pixels)
        {
            sum += pixel;
        }

        var average = (byte)(sum / TotalBits);
        ulong value = 0;

        for (var index = 0; index < pixels.Length; index++)
        {
            if (pixels[index] > average)
            {
                value |= 1UL << (TotalBits - 1 - index);
            }
        }

        return new ImageHash(value, TotalBits);
    }
}
