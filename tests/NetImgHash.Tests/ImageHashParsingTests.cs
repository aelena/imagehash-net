namespace NetImgHash.Tests;

/// <summary>
/// Parsing, formatting and equality of <see cref="ImageHash"/>, beyond the invariants
/// covered in <see cref="ImageHashInvariantTests"/>.
/// </summary>
public sealed class ImageHashParsingTests
{
    // --- bit length is an argument, not a format ---------------------------------------

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(65)]
    public void Parse_RejectsAnOutOfRangeBitLengthAsAnArgumentError(int bitLength)
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => ImageHash.Parse("ff", bitLength));

        Assert.Equal("bitLength", ex.ParamName);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(65)]
    public void TryParse_ReturnsFalseForAnOutOfRangeBitLength(int bitLength)
    {
        Assert.False(ImageHash.TryParse("ff", bitLength, out var hash));
        Assert.True(hash.IsEmpty);
    }

    // --- exact digit count ---------------------------------------------------------------

    [Theory]
    [InlineData("ff", 64)]                    // too short for 64 bits
    [InlineData("00000000000000ff", 8)]       // too long for 8 bits, even though the value fits
    [InlineData("0ff", 8)]                    // one extra leading zero
    [InlineData("0123456789abcdef0", 64)]     // 17 digits
    [InlineData("f", 8)]                      // one digit short
    public void TryParse_RequiresExactlyTheDigitsTheBitLengthImplies(string hex, int bitLength) =>
        Assert.False(ImageHash.TryParse(hex, bitLength, out _));

    [Theory]
    [InlineData("ff", 64)]
    [InlineData("0ff", 8)]
    public void Parse_NamesTheExpectedDigitCountWhenTheLengthIsWrong(string hex, int bitLength)
    {
        var ex = Assert.Throws<FormatException>(() => ImageHash.Parse(hex, bitLength));

        Assert.Contains("exactly", ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("ff", 5)]   // two digits is the right width for 5 bits, but 0xff needs 8
    [InlineData("8", 3)]    // one digit, yet 0x8 does not fit in 3 bits
    [InlineData("3ff", 9)]  // 0x3ff is 1023; nine bits hold at most 511
    public void TryParse_RejectsAValueThatOverflowsAnUnalignedBitLength(string hex, int bitLength)
    {
        Assert.False(ImageHash.TryParse(hex, bitLength, out _));
        Assert.Throws<FormatException>(() => ImageHash.Parse(hex, bitLength));
    }

    [Theory]
    [InlineData(1, "1")]
    [InlineData(4, "f")]
    [InlineData(5, "1f")]
    [InlineData(8, "ff")]
    [InlineData(12, "fff")]
    [InlineData(63, "7fffffffffffffff")]
    [InlineData(64, "ffffffffffffffff")]
    public void Parse_AcceptsTheFullWidthStringForAnyBitLength(int bitLength, string hex)
    {
        var hash = ImageHash.Parse(hex, bitLength);

        Assert.Equal(bitLength, hash.BitLength);
        Assert.Equal(hex, hash.ToString());
    }

    [Fact]
    public void ToString_ThenParse_RoundTripsEveryBitLength()
    {
        for (var bitLength = 1; bitLength <= 64; bitLength++)
        {
            var value = bitLength == 64 ? 0x9E3779B97F4A7C15UL : 0x9E3779B97F4A7C15UL & ((1UL << bitLength) - 1);
            var original = new ImageHash(value, bitLength);

            var parsed = ImageHash.Parse(original.ToString(), bitLength);

            Assert.Equal(original, parsed);
        }
    }

    // --- lenient about case, prefix and surrounding whitespace ---------------------------

    [Theory]
    [InlineData("0xa1b2c3d4e5f60718")]
    [InlineData("0XA1B2C3D4E5F60718")]
    [InlineData("A1B2C3D4E5F60718")]
    [InlineData("  a1b2c3d4e5f60718  ")]
    [InlineData("\ta1b2c3d4e5f60718\n")]
    public void TryParse_NormalisesCasePrefixAndWhitespace(string input)
    {
        Assert.True(ImageHash.TryParse(input, 64, out var hash));
        Assert.Equal("a1b2c3d4e5f60718", hash.ToString());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("0x")]
    [InlineData("zzzzzzzzzzzzzzzz")]
    [InlineData("a1b2c3d4e5f6071g")]
    [InlineData("-1b2c3d4e5f60718")]
    [InlineData("a1b2 c3d4e5f60718")]
    public void TryParse_ReturnsFalseForNonHexInput(string? input) =>
        Assert.False(ImageHash.TryParse(input, 64, out _));

    // --- equality ------------------------------------------------------------------------

    [Fact]
    public void Equality_ComparesValueAndBitLength()
    {
        var a = new ImageHash(0xff, 8);
        var same = new ImageHash(0xff, 8);
        var widerSameValue = new ImageHash(0xff, 16);
        var differentValue = new ImageHash(0xfe, 8);

        Assert.True(a == same);
        Assert.False(a != same);
        Assert.True(a.Equals(same));
        Assert.True(a.Equals((object)same));
        Assert.Equal(a.GetHashCode(), same.GetHashCode());

        Assert.True(a != widerSameValue);
        Assert.False(a == widerSameValue);
        Assert.NotEqual(a, widerSameValue);

        Assert.True(a != differentValue);
        Assert.NotEqual(a.GetHashCode(), differentValue.GetHashCode());
    }

    [Fact]
    public void Equals_ReturnsFalseForNullAndForOtherTypes()
    {
        var hash = new ImageHash(0xff, 8);

        Assert.False(hash.Equals(null));
        Assert.False(hash.Equals("ff"));
        Assert.False(hash.Equals(0xffUL));
    }

    [Fact]
    public void Default_EqualsDefaultAndNothingElse()
    {
        ImageHash empty = default;

        Assert.True(empty == default);
        Assert.False(empty == new ImageHash(0, 1));
        Assert.True(empty.Equals((object)default(ImageHash)));
    }

    [Fact]
    public void Hashes_WorkAsDictionaryKeys()
    {
        var seen = new Dictionary<ImageHash, string>
        {
            [ImageHash.Parse("a1b2c3d4e5f60718")] = "first",
        };

        Assert.Equal("first", seen[ImageHash.Parse("0xA1B2C3D4E5F60718")]);
        Assert.False(seen.ContainsKey(ImageHash.Parse("a1b2c3d4e5f60719")));
    }
}
