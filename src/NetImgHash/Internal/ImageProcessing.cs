using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace NetImgHash.Internal;

internal static class ImageProcessing
{
    private const int RedCoefficient = 299;
    private const int GreenCoefficient = 587;
    private const int BlueCoefficient = 114;

    public static byte[] LoadResizedGrayscaleBuffer(Stream stream, int width, int height)
    {
        ArgumentNullException.ThrowIfNull(stream);

        using var image = Image.Load<Rgba32>(stream);
        var grayscalePixels = new byte[image.Width * image.Height];
        var grayscaleIndex = 0;

        image.ProcessPixelRows(accessor =>
        {
            for (var y = 0; y < accessor.Height; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (var x = 0; x < row.Length; x++)
                {
                    grayscalePixels[grayscaleIndex++] = ToPillowLuma(row[x]);
                }
            }
        });

        using var grayscale = Image.LoadPixelData<L8>(grayscalePixels, image.Width, image.Height);
        grayscale.Mutate(context => context.Resize(width, height, KnownResamplers.Lanczos3));

        var pixels = new byte[width * height];
        var index = 0;

        grayscale.ProcessPixelRows(accessor =>
        {
            for (var y = 0; y < accessor.Height; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (var x = 0; x < row.Length; x++)
                {
                    pixels[index++] = row[x].PackedValue;
                }
            }
        });

        return pixels;
    }

    private static byte ToPillowLuma(Rgba32 pixel)
    {
        var luminance = (RedCoefficient * pixel.R)
            + (GreenCoefficient * pixel.G)
            + (BlueCoefficient * pixel.B);

        return (byte)Math.Clamp((luminance + 500) / 1000, 0, 255);
    }
}
