using AwesomeAssertions;

namespace NetImgHash.Tests;

/// <summary>
/// The invariant that <see cref="ImageHash.Value"/> carries no bits above
/// <see cref="ImageHash.BitLength"/>.
/// </summary>
/// <remarks>
/// Without it, <c>ImageHash.Parse("ffffffffffffffff", 8)</c> produced a hash reporting
/// 8 bits while holding 64. <see cref="ImageHash.HammingDistance"/> counts bits across
/// the whole word, so it returned 64 for an 8-bit hash and <see cref="ImageHash.Similarity"/>
/// returned -7 — outside the 0..1 range its own documentation promises. The hash also
/// printed 16 hex digits despite claiming to pad to its bit length.
/// </remarks>
public sealed class ImageHashInvariantTests
{
    [Theory]
    [InlineData(0xFFFFFFFFFFFFFFFF, 8)]
    [InlineData(0x100, 8)]
    [InlineData(0x2, 1)]
    [InlineData(0x10000, 16)]
    public void Constructor_RejectsValueWiderThanBitLength(ulong value, int bitLength) =>
        FluentActions.Invoking(() => new ImageHash(value, bitLength))
            .Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName(nameof(value));

    [Theory]
    [InlineData(0xFF, 8)]
    [InlineData(0x1, 1)]
    [InlineData(0xFFFF, 16)]
    [InlineData(0xFFFFFFFFFFFFFFFF, 64)]
    public void Constructor_AcceptsValueThatFits(ulong value, int bitLength) =>
        new ImageHash(value, bitLength).Value.Should().Be(value);

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(65)]
    public void Constructor_RejectsOutOfRangeBitLength(int bitLength) =>
        FluentActions.Invoking(() => new ImageHash(1UL, bitLength))
            .Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName(nameof(bitLength));

    [Fact]
    public void Parse_RejectsHashWiderThanTheRequestedBitLength() =>
        FluentActions.Invoking(() => ImageHash.Parse("ffffffffffffffff", 8))
            .Should().Throw<FormatException>();

    [Fact]
    public void TryParse_ReturnsFalseForHashWiderThanBitLength()
    {
        ImageHash.TryParse("ffffffffffffffff", 8, out var hash).Should().BeFalse();
        hash.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void Similarity_StaysWithinZeroToOne()
    {
        // The pair that used to yield -7.
        var allSet = new ImageHash(0xFF, 8);
        var allClear = new ImageHash(0x00, 8);

        allSet.HammingDistance(allClear).Should().Be(8);
        allSet.Similarity(allClear).Should().Be(0d);
        allSet.Similarity(allSet).Should().Be(1d);
    }

    [Theory]
    [InlineData(64, 16)]
    [InlineData(32, 8)]
    [InlineData(16, 4)]
    [InlineData(8, 2)]
    [InlineData(4, 1)]
    public void ToString_PadsToExactlyTheDeclaredBitLength(int bitLength, int expectedDigits) =>
        new ImageHash(1UL, bitLength).ToString().Should().HaveLength(expectedDigits);

    [Fact]
    public void ToString_IsLowercaseHex() =>
        new ImageHash(0xABCDEF, 64).ToString().Should().Be("0000000000abcdef");

    // ── The uninitialized default ────────────────────────────────────────
    //
    // A struct can always be default-constructed past the constructor's checks,
    // so `default(ImageHash)` has BitLength 0. Similarity then divided by zero
    // and returned NaN rather than failing.

    [Fact]
    public void Default_IsReportedAsEmpty()
    {
        default(ImageHash).IsEmpty.Should().BeTrue();
        new ImageHash(1UL, 64).IsEmpty.Should().BeFalse();
    }

    [Fact]
    public void Default_SimilarityThrowsRatherThanReturningNaN() =>
        FluentActions.Invoking(() => default(ImageHash).Similarity(default))
            .Should().Throw<InvalidOperationException>()
            .WithMessage("*not a computed hash*");

    [Fact]
    public void Default_HammingDistanceThrows() =>
        FluentActions.Invoking(() => default(ImageHash).HammingDistance(new ImageHash(1UL, 64)))
            .Should().Throw<InvalidOperationException>();

    [Fact]
    public void Default_ComparedAgainstARealHashThrows() =>
        FluentActions.Invoking(() => new ImageHash(1UL, 64).HammingDistance(default))
            .Should().Throw<InvalidOperationException>();

    [Fact]
    public void Default_ToStringIsEmptyRatherThanMisleading() =>
        default(ImageHash).ToString().Should().BeEmpty();

    // ── Mismatched lengths ───────────────────────────────────────────────

    [Fact]
    public void HammingDistance_MismatchedBitLengths_NamesBothLengths() =>
        FluentActions.Invoking(() => new ImageHash(1UL, 64).HammingDistance(new ImageHash(1UL, 16)))
            .Should().Throw<ArgumentException>()
            .WithMessage("*64*16*");

    // ── Round-tripping ───────────────────────────────────────────────────

    [Theory]
    [InlineData("0000000000000000")]
    [InlineData("ffffffffffffffff")]
    [InlineData("8000000000000001")]
    [InlineData("a1b2c3d4e5f60718")]
    public void Parse_RoundTripsThroughToString(string hex) =>
        ImageHash.Parse(hex).ToString().Should().Be(hex);

    [Fact]
    public void Parse_AcceptsA0xPrefix() =>
        ImageHash.Parse("0xff", 8).Should().Be(new ImageHash(0xFF, 8));
}
