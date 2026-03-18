namespace NetImgHash.Tests;

public sealed class ImageHashTests
{
    [Fact]
    public void ToString_UsesLowercaseHexAndPadsToBitLength()
    {
        var hash = new ImageHash(0x1A2BUL, 64);

        Assert.Equal("0000000000001a2b", hash.ToString());
    }

    [Fact]
    public void Parse_RoundTripsHexValue()
    {
        var hash = ImageHash.Parse("8000000000000001");

        Assert.Equal(new ImageHash(0x8000000000000001UL, 64), hash);
    }

    [Fact]
    public void HammingDistance_IsSymmetric()
    {
        var left = new ImageHash(0b1010UL, 4);
        var right = new ImageHash(0b1110UL, 4);

        Assert.Equal(1, left.HammingDistance(right));
        Assert.Equal(left.HammingDistance(right), right.HammingDistance(left));
    }

    [Fact]
    public void Similarity_NormalizesAgainstBitLength()
    {
        var left = new ImageHash(0b1111UL, 4);
        var right = new ImageHash(0b1100UL, 4);

        Assert.Equal(0.5d, left.Similarity(right), 6);
    }

    [Fact]
    public void HammingDistance_ThrowsForMismatchedBitLengths()
    {
        var left = new ImageHash(1UL, 64);
        var right = new ImageHash(1UL, 16);

        Assert.Throws<ArgumentException>(() => left.HammingDistance(right));
    }
}
