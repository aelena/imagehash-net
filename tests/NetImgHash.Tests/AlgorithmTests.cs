using NetImgHash.Tests.Helpers;
using SixLabors.ImageSharp.PixelFormats;

namespace NetImgHash.Tests;

public sealed class AlgorithmTests
{
    [Fact]
    public void AverageHash_UsesRowMajorMostSignificantBitPacking()
    {
        using var stream = TestImageFactory.CreatePngStream(
            8,
            8,
            (x, y) => x == 0 && y == 0 ? new Rgba32(255, 255, 255) : new Rgba32(0, 0, 0));

        var hash = ImageHasher.Compute(stream, HashAlgorithm.AverageHash);

        Assert.Equal("8000000000000000", hash.ToString());
    }

    [Fact]
    public void AverageHash_ProducesZeroForUniformImage()
    {
        using var stream = TestImageFactory.CreatePngStream(8, 8, (_, _) => new Rgba32(32, 32, 32));

        var hash = ImageHasher.Compute(stream, HashAlgorithm.AverageHash);

        Assert.Equal("0000000000000000", hash.ToString());
    }

    [Fact]
    public void DifferenceHash_UsesRowMajorMostSignificantBitPacking()
    {
        using var stream = TestImageFactory.CreatePngStream(
            9,
            8,
            (x, y) => x == 1 && y == 0 ? new Rgba32(255, 255, 255) : new Rgba32(0, 0, 0));

        var hash = ImageHasher.Compute(stream, HashAlgorithm.DifferenceHash);

        Assert.Equal("8000000000000000", hash.ToString());
    }

    [Fact]
    public void DifferenceHash_ProducesZeroForUniformImage()
    {
        using var stream = TestImageFactory.CreatePngStream(9, 8, (_, _) => new Rgba32(128, 128, 128));

        var hash = ImageHasher.Compute(stream, HashAlgorithm.DifferenceHash);

        Assert.Equal("0000000000000000", hash.ToString());
    }

    // pHash expectations below come from Python imagehash 4.3.2 (Pillow 12.1.1) on
    // 32×32 inputs, so no resampling is involved and only the DCT and median are under test.

    [Fact]
    public void PerceptualHash_UniformImageSetsOnlyTheDcBit()
    {
        // Every AC coefficient is 0 and so is the median; only the DC term exceeds it.
        using var stream = TestImageFactory.CreatePngStream(32, 32, (_, _) => new Rgba32(32, 32, 32));

        var hash = ImageHasher.Compute(stream, HashAlgorithm.PerceptualHash);

        Assert.Equal("8000000000000000", hash.ToString());
    }

    // A lone pixel at (0,0) is deliberately absent: its DCT block is symmetric, the two
    // middle coefficients are mathematically equal, and the reference resolves that tie
    // by floating-point noise (a 2e-13 gap). The inputs below have margins of at least
    // 14 units between every coefficient and the median.

    [Fact]
    public void PerceptualHash_TwoBrightPixelsMatchReference()
    {
        using var stream = TestImageFactory.CreatePngStream(
            32,
            32,
            (x, y) => (x, y) is (3, 0) or (0, 7) ? new Rgba32(255, 255, 255) : new Rgba32(0, 0, 0));

        var hash = ImageHasher.Compute(stream, HashAlgorithm.PerceptualHash);

        Assert.Equal("fffef8c00000f0fc", hash.ToString());
    }

    [Fact]
    public void PerceptualHash_BrightCornerBlockMatchesReference()
    {
        using var stream = TestImageFactory.CreatePngStream(
            32,
            32,
            (x, y) => x < 5 && y < 11 ? new Rgba32(255, 255, 255) : new Rgba32(0, 0, 0));

        var hash = ImageHasher.Compute(stream, HashAlgorithm.PerceptualHash);

        Assert.Equal("fefefe000100f0fc", hash.ToString());
    }

    [Fact]
    public void PerceptualHash_CheckerboardMatchesReference()
    {
        using var stream = TestImageFactory.CreatePngStream(
            32,
            32,
            (x, y) => ((x / 4) + (y / 4)) % 2 == 0 ? new Rgba32(255, 255, 255) : new Rgba32(0, 0, 0));

        var hash = ImageHasher.Compute(stream, HashAlgorithm.PerceptualHash);

        Assert.Equal("8055005500550055", hash.ToString());
    }
}
