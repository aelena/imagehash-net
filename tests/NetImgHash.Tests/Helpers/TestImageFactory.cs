using System.Buffers.Binary;
using System.IO.Compression;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;

namespace NetImgHash.Tests.Helpers;

internal static class TestImageFactory
{
    public static MemoryStream CreatePngStream(int width, int height, Func<int, int, Rgba32> pixelFactory)
    {
        using var image = CreateImage(width, height, pixelFactory);

        var stream = new MemoryStream();
        image.Save(stream, new PngEncoder());
        stream.Position = 0;
        return stream;
    }

    /// <summary>
    /// An animated GIF whose frames are given by successive pixel factories. Two-colour
    /// frames survive GIF's palette quantisation exactly, so hashes are deterministic.
    /// </summary>
    public static MemoryStream CreateGifStream(int width, int height, params Func<int, int, Rgba32>[] frameFactories)
    {
        using var image = CreateImage(width, height, frameFactories[0]);
        foreach (var factory in frameFactories.Skip(1))
        {
            using var frame = CreateImage(width, height, factory);
            image.Frames.AddFrame(frame.Frames.RootFrame);
        }

        var stream = new MemoryStream();
        image.Save(stream, new GifEncoder());
        stream.Position = 0;
        return stream;
    }

    /// <summary>
    /// A syntactically valid PNG whose header claims the given dimensions but which
    /// carries almost no pixel data: the shape of a decompression bomb. Under 100 bytes
    /// regardless of the dimensions claimed.
    /// </summary>
    public static MemoryStream CreatePngHeaderClaiming(int width, int height)
    {
        var stream = new MemoryStream();
        stream.Write([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);

        var ihdr = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(ihdr.AsSpan(0, 4), width);
        BinaryPrimitives.WriteInt32BigEndian(ihdr.AsSpan(4, 4), height);
        ihdr[8] = 8;  // bit depth
        ihdr[9] = 6;  // colour type: RGBA
        WriteChunk(stream, "IHDR", ihdr);

        using var idat = new MemoryStream();
        using (var zlib = new ZLibStream(idat, CompressionLevel.Fastest, leaveOpen: true))
        {
            zlib.Write(new byte[64]);
        }

        WriteChunk(stream, "IDAT", idat.ToArray());
        WriteChunk(stream, "IEND", []);

        stream.Position = 0;
        return stream;
    }

    private static Image<Rgba32> CreateImage(int width, int height, Func<int, int, Rgba32> pixelFactory)
    {
        var image = new Image<Rgba32>(width, height);

        image.ProcessPixelRows(accessor =>
        {
            for (var y = 0; y < accessor.Height; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (var x = 0; x < row.Length; x++)
                {
                    row[x] = pixelFactory(x, y);
                }
            }
        });

        return image;
    }

    private static void WriteChunk(Stream stream, string type, byte[] data)
    {
        var typeBytes = System.Text.Encoding.ASCII.GetBytes(type);
        var length = new byte[4];
        BinaryPrimitives.WriteInt32BigEndian(length, data.Length);
        stream.Write(length);
        stream.Write(typeBytes);
        stream.Write(data);

        var crcInput = new byte[typeBytes.Length + data.Length];
        typeBytes.CopyTo(crcInput, 0);
        data.CopyTo(crcInput, typeBytes.Length);
        var crc = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(crc, Crc32(crcInput));
        stream.Write(crc);
    }

    private static uint Crc32(ReadOnlySpan<byte> data)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var b in data)
        {
            crc ^= b;
            for (var i = 0; i < 8; i++)
            {
                crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320u : crc >> 1;
            }
        }

        return ~crc;
    }
}
