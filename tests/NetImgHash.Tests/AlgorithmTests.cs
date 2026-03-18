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
}
