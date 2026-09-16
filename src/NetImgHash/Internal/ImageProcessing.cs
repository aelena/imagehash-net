using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace NetImgHash.Internal;

/// <summary>
/// The pre-processing every hash shares: decode, Pillow's grayscale conversion, then
/// Pillow's Lanczos resize. ImageSharp is used to decode only; everything after the
/// RGBA pixels is reproduced from Pillow so the bits match Python <c>imagehash</c>.
/// </summary>
internal static class ImageProcessing
{
    public static byte[] LoadResizedGrayscaleBuffer(Stream stream, int width, int height)
    {
        ArgumentNullException.ThrowIfNull(stream);

        using var image = Image.Load<Rgba32>(stream);
        var grayscale = new byte[image.Width * image.Height];
        var index = 0;

        image.ProcessPixelRows(accessor =>
        {
            for (var y = 0; y < accessor.Height; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (var x = 0; x < row.Length; x++)
                {
                    grayscale[index++] = ToPillowLuma(row[x]);
                }
            }
        });

        return PillowResampler.ResizeLanczos(grayscale, image.Width, image.Height, width, height);
    }

    /// <summary>
    /// Pillow's <c>convert("L")</c>: ITU-R 601-2 luma in 16.16 fixed point,
    /// <c>(R·19595 + G·38470 + B·7471 + 0x8000) &gt;&gt; 16</c>. Alpha is ignored, as
    /// Pillow ignores it. The integer weights are what Pillow uses, not the textbook
    /// 299/587/114 per mille; the two round differently for some inputs.
    /// </summary>
    private static byte ToPillowLuma(Rgba32 pixel)
    {
        var luminance = (pixel.R * 19595) + (pixel.G * 38470) + (pixel.B * 7471) + 0x8000;
        return (byte)(luminance >> 16);
    }
}
