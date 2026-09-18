using NetImgHash.Tests.Helpers;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace NetImgHash.Tests;

public sealed class ImageHasherTests
{
    private static readonly Rgba32 White = new(255, 255, 255);
    private static readonly Rgba32 Black = new(0, 0, 0);

    private static Rgba32 RightHalfWhite(int x, int _) => x >= 4 ? White : Black;

    private static Rgba32 BottomHalfWhite(int _, int y) => y >= 4 ? White : Black;

    // --- argument validation ---------------------------------------------------------

    [Fact]
    public void WaveletHash_IsReservedAndThrowsNotSupported()
    {
        using var stream = TestImageFactory.CreatePngStream(8, 8, RightHalfWhite);

        Assert.Throws<NotSupportedException>(() => ImageHasher.Compute(stream, HashAlgorithm.WaveletHash));
    }

    [Fact]
    public void UndefinedAlgorithm_ThrowsArgumentOutOfRange()
    {
        using var stream = TestImageFactory.CreatePngStream(8, 8, RightHalfWhite);

        Assert.Throws<ArgumentOutOfRangeException>(() => ImageHasher.Compute(stream, (HashAlgorithm)42));
    }

    [Fact]
    public void NullStream_ThrowsArgumentNull() =>
        Assert.Throws<ArgumentNullException>(() => ImageHasher.Compute((Stream)null!, HashAlgorithm.AverageHash));

    [Fact]
    public void NullOptions_ThrowsArgumentNull()
    {
        using var stream = TestImageFactory.CreatePngStream(8, 8, RightHalfWhite);

        Assert.Throws<ArgumentNullException>(() => ImageHasher.Compute(stream, HashAlgorithm.AverageHash, null!));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void NullOrBlankPath_ThrowsArgument(string? path) =>
        Assert.ThrowsAny<ArgumentException>(() => ImageHasher.Compute(path!, HashAlgorithm.AverageHash));

    [Fact]
    public void MissingFile_ThrowsFileNotFound()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "TestData", "Images", "does-not-exist.png");

        Assert.Throws<FileNotFoundException>(() => ImageHasher.Compute(path, HashAlgorithm.AverageHash));
    }

    [Fact]
    public void NonImageBytes_ThrowUnknownImageFormat()
    {
        using var stream = new MemoryStream("hello, this is not an image"u8.ToArray());

        Assert.Throws<UnknownImageFormatException>(() => ImageHasher.Compute(stream, HashAlgorithm.AverageHash));
    }

    // --- streams ---------------------------------------------------------------------

    [Fact]
    public void NonSeekableStream_ProducesTheSameHashAsASeekableOne()
    {
        using var seekable = TestImageFactory.CreatePngStream(8, 8, RightHalfWhite);
        var expected = ImageHasher.Compute(seekable, HashAlgorithm.AverageHash);
        seekable.Position = 0;

        using var nonSeekable = new NonSeekableStream(seekable);
        var actual = ImageHasher.Compute(nonSeekable, HashAlgorithm.AverageHash);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Stream_IsReadFromItsCurrentPositionAndLeftOpen()
    {
        using var image = TestImageFactory.CreatePngStream(8, 8, RightHalfWhite);
        using var stream = new MemoryStream();
        stream.Write(new byte[100]);            // unrelated leading bytes
        var start = stream.Position;
        image.CopyTo(stream);
        stream.Position = start;

        var hash = ImageHasher.Compute(stream, HashAlgorithm.AverageHash);

        Assert.Equal("0f0f0f0f0f0f0f0f", hash.ToString());
        Assert.True(stream.CanRead);
    }

    [Fact]
    public void PathOverload_MatchesStreamOverload()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "TestData", "Images", "standard-photo.png");
        using var stream = File.OpenRead(path);

        var fromPath = ImageHasher.Compute(path, HashAlgorithm.DifferenceHash, ImageHashOptions.Default);
        var fromStream = ImageHasher.Compute(stream, HashAlgorithm.DifferenceHash);

        Assert.Equal(fromStream, fromPath);
    }

    // --- pixel budget ----------------------------------------------------------------

    [Theory]
    [InlineData(20_000, 20_000)]
    [InlineData(40_000, 40_000)]
    [InlineData(1, 60_000_000)]
    public void OversizedHeader_IsRejectedBeforeDecoding(int width, int height)
    {
        // A file under 100 bytes that claims hundreds of megapixels. Before the budget
        // existed the 20000² case allocated 2.2 GB and took 18 s to return a hash.
        using var bomb = TestImageFactory.CreatePngHeaderClaiming(width, height);

        var ex = Assert.Throws<ImageTooLargeException>(() => ImageHasher.Compute(bomb, HashAlgorithm.AverageHash));

        Assert.Equal(width, ex.Width);
        Assert.Equal(height, ex.Height);
        Assert.Equal(ImageHashOptions.DefaultMaxPixels, ex.MaxPixels);
        Assert.Contains("MaxPixels", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void OversizedHeader_IsRejectedForEveryAlgorithm()
    {
        foreach (var algorithm in new[] { HashAlgorithm.AverageHash, HashAlgorithm.DifferenceHash, HashAlgorithm.PerceptualHash })
        {
            using var bomb = TestImageFactory.CreatePngHeaderClaiming(30_000, 30_000);

            Assert.Throws<ImageTooLargeException>(() => ImageHasher.Compute(bomb, algorithm));
        }
    }

    [Fact]
    public void CustomBudget_RejectsAnImageJustAboveIt()
    {
        using var stream = TestImageFactory.CreatePngStream(8, 8, RightHalfWhite);
        var options = new ImageHashOptions { MaxPixels = 63 };

        var ex = Assert.Throws<ImageTooLargeException>(() => ImageHasher.Compute(stream, HashAlgorithm.AverageHash, options));

        Assert.Equal(63, ex.MaxPixels);
    }

    [Fact]
    public void CustomBudget_AcceptsAnImageExactlyAtIt()
    {
        using var stream = TestImageFactory.CreatePngStream(8, 8, RightHalfWhite);
        var options = new ImageHashOptions { MaxPixels = 64 };

        var hash = ImageHasher.Compute(stream, HashAlgorithm.AverageHash, options);

        Assert.Equal("0f0f0f0f0f0f0f0f", hash.ToString());
    }

    [Fact]
    public void Budget_IsCheckedOnANonSeekableStreamToo()
    {
        using var bomb = TestImageFactory.CreatePngHeaderClaiming(20_000, 20_000);
        using var nonSeekable = new NonSeekableStream(bomb);

        Assert.Throws<ImageTooLargeException>(() => ImageHasher.Compute(nonSeekable, HashAlgorithm.AverageHash));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(long.MinValue)]
    public void Options_RejectANonPositiveBudget(long maxPixels) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new ImageHashOptions { MaxPixels = maxPixels });

    [Fact]
    public void Options_DefaultIsFiftyMegapixels()
    {
        Assert.Equal(50_000_000, ImageHashOptions.DefaultMaxPixels);
        Assert.Equal(ImageHashOptions.DefaultMaxPixels, ImageHashOptions.Default.MaxPixels);
        Assert.Equal(ImageHashOptions.DefaultMaxPixels, new ImageHashOptions().MaxPixels);
    }

    [Fact]
    public void ImageTooLargeException_StandardConstructorsWork()
    {
        var inner = new InvalidOperationException("inner");

        Assert.Contains("budget", new ImageTooLargeException().Message, StringComparison.Ordinal);
        Assert.Equal("custom", new ImageTooLargeException("custom").Message);
        Assert.Same(inner, new ImageTooLargeException("custom", inner).InnerException);
    }

    // --- multi-frame images ----------------------------------------------------------

    [Fact]
    public void AnimatedGif_HashesTheFirstFrameOnly()
    {
        using var firstFrameAlone = TestImageFactory.CreatePngStream(8, 8, RightHalfWhite);
        using var lastFrameAlone = TestImageFactory.CreatePngStream(8, 8, BottomHalfWhite);
        using var animation = TestImageFactory.CreateGifStream(8, 8, RightHalfWhite, BottomHalfWhite, BottomHalfWhite);

        var expected = ImageHasher.Compute(firstFrameAlone, HashAlgorithm.AverageHash);
        var other = ImageHasher.Compute(lastFrameAlone, HashAlgorithm.AverageHash);
        var actual = ImageHasher.Compute(animation, HashAlgorithm.AverageHash);

        Assert.Equal(expected, actual);
        Assert.NotEqual(other, actual);
    }
}
