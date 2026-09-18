using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.PixelFormats;

namespace NetImgHash.Internal;

/// <summary>
/// The pre-processing every hash shares: decode, Pillow's grayscale conversion, then
/// Pillow's Lanczos resize. ImageSharp is used to decode only; everything after the
/// RGBA pixels is reproduced from Pillow so the bits match Python <c>imagehash</c>.
/// </summary>
internal static class ImageProcessing
{
    public static byte[] LoadResizedGrayscaleBuffer(Stream stream, int width, int height, ImageHashOptions options)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(options);

        if (stream.CanSeek)
        {
            return Load(stream, width, height, options);
        }

        // The header is read twice (identify, then decode), which needs to seek.
        // ImageSharp buffers a non-seekable stream into memory anyway, so this costs
        // nothing extra, and the file size is bounded by whoever handed us the stream.
        using var buffered = new MemoryStream();
        stream.CopyTo(buffered);
        buffered.Position = 0;
        return Load(buffered, width, height, options);
    }

    private static byte[] Load(Stream stream, int width, int height, ImageHashOptions options)
    {
        // Only the first frame is ever hashed, which is also what Pillow hands
        // imagehash. Decoding every frame of an animation multiplies memory by the
        // frame count for nothing.
        var decoderOptions = new DecoderOptions { MaxFrames = 1 };

        // The header is checked against the pixel budget before any pixel buffer
        // exists. Decoders trust the declared dimensions, so without this a 70-byte
        // file can demand gigabytes.
        var start = stream.Position;
        var info = Image.Identify(decoderOptions, stream);
        stream.Position = start;

        if ((long)info.Width * info.Height > options.MaxPixels)
        {
            throw new ImageTooLargeException(info.Width, info.Height, options.MaxPixels);
        }

        using var image = Image.Load<Rgba32>(decoderOptions, stream);
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
