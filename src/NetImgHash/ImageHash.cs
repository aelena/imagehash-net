using System.Globalization;
using System.Numerics;

namespace NetImgHash;

/// <summary>
/// Represents a perceptual image hash value.
/// </summary>
/// <remarks>
/// <see cref="Value"/> is guaranteed to carry no bits above <see cref="BitLength"/>.
/// Without that invariant a hash could claim eight bits while holding sixty-four, and
/// <see cref="HammingDistance"/> — which counts bits across the whole word — would
/// return a distance larger than the hash itself, giving a negative similarity.
/// </remarks>
public readonly struct ImageHash : IEquatable<ImageHash>
{
    private const int MaxBits = 64;
    private const int MaxHexDigits = MaxBits / 4;

    /// <summary>
    /// Initializes a new instance of the <see cref="ImageHash"/> struct.
    /// </summary>
    /// <param name="value">The packed hash bits.</param>
    /// <param name="bitLength">The number of significant bits in <paramref name="value"/>.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// When <paramref name="bitLength"/> is outside 1 to 64, or <paramref name="value"/> has
    /// bits set above <paramref name="bitLength"/>.
    /// </exception>
    public ImageHash(ulong value, int bitLength)
    {
        if (bitLength is <= 0 or > MaxBits)
        {
            throw new ArgumentOutOfRangeException(
                nameof(bitLength), bitLength, $"Bit length must be between 1 and {MaxBits}.");
        }

        if ((value & ~MaskFor(bitLength)) != 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                $"0x{value:x} does not fit in {bitLength} bits. A hash carrying more bits than "
                + "it declares produces distances longer than its own length.");
        }

        Value = value;
        BitLength = bitLength;
    }

    /// <summary>
    /// Gets the packed hash bits. No bit above <see cref="BitLength"/> is ever set.
    /// </summary>
    public ulong Value { get; }

    /// <summary>
    /// Gets the number of significant bits in <see cref="Value"/>.
    /// Zero only for <c>default(ImageHash)</c>, which is not a usable hash.
    /// </summary>
    public int BitLength { get; }

    /// <summary>
    /// Gets a value indicating whether this is the uninitialized <c>default</c> value
    /// rather than a computed hash.
    /// </summary>
    public bool IsEmpty => BitLength == 0;

    /// <summary>
    /// Computes the Hamming distance between this hash and another hash.
    /// </summary>
    /// <exception cref="ArgumentException">When the bit lengths differ.</exception>
    /// <exception cref="InvalidOperationException">When either hash is <c>default</c>.</exception>
    public int HammingDistance(ImageHash other)
    {
        ThrowIfEmpty(this);
        ThrowIfEmpty(other);

        if (BitLength != other.BitLength)
        {
            throw new ArgumentException(
                $"Hash bit lengths must match; this hash is {BitLength} bits and the other is "
                + $"{other.BitLength}. Hashes of different lengths are not comparable.",
                nameof(other));
        }

        return BitOperations.PopCount(Value ^ other.Value);
    }

    /// <summary>
    /// Returns a similarity score between 0.0 (every bit differs) and 1.0 (identical).
    /// </summary>
    /// <exception cref="ArgumentException">When the bit lengths differ.</exception>
    /// <exception cref="InvalidOperationException">When either hash is <c>default</c>.</exception>
    public double Similarity(ImageHash other) =>
        1d - ((double)HammingDistance(other) / BitLength);

    /// <summary>
    /// Parses a hexadecimal hash string into an <see cref="ImageHash"/>.
    /// </summary>
    /// <param name="value">Hexadecimal digits, optionally prefixed with <c>0x</c>.</param>
    /// <param name="bitLength">Expected significant bits; the value must fit within it.</param>
    /// <exception cref="FormatException">
    /// When the string is not hexadecimal, or holds more bits than <paramref name="bitLength"/>.
    /// </exception>
    public static ImageHash Parse(string value, int bitLength = MaxBits)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        // A bad bit length is the caller's argument, not the string's format, and is
        // reported as such before the string is looked at.
        if (bitLength is <= 0 or > MaxBits)
        {
            throw new ArgumentOutOfRangeException(
                nameof(bitLength), bitLength, $"Bit length must be between 1 and {MaxBits}.");
        }

        if (!TryParse(value, bitLength, out var hash))
        {
            throw new FormatException(
                $"'{value}' is not a valid {bitLength}-bit hexadecimal hash: expected exactly "
                + $"{HexDigitsFor(bitLength)} hexadecimal digits, optionally prefixed with 0x.");
        }

        return hash;
    }

    /// <summary>
    /// Tries to parse a hexadecimal hash string into an <see cref="ImageHash"/>.
    /// </summary>
    /// <returns>
    /// <c>false</c> when the string is not hexadecimal, when <paramref name="bitLength"/> is
    /// outside 1 to 64, when the string does not have exactly the number of hex digits
    /// that bit length implies, or when the parsed value does not fit in that many bits.
    /// </returns>
    public static bool TryParse(string? value, int bitLength, out ImageHash hash)
    {
        hash = default;

        if (string.IsNullOrWhiteSpace(value) || bitLength is <= 0 or > MaxBits)
        {
            return false;
        }

        var normalized = value.Trim();

        if (normalized.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[2..];
        }

        // Exactly the digits the bit length implies, no fewer. ToString always pads, and
        // so does Python imagehash, so a shorter string is a stored hash that lost its
        // leading zeros or was cut short; either way it is not the hash it claims to be.
        if (normalized.Length != HexDigitsFor(bitLength))
        {
            return false;
        }

        if (!ulong.TryParse(normalized, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var parsed))
        {
            return false;
        }

        // Rejected, not truncated. "ffffffffffffffff" read as an 8-bit hash is a caller
        // mistake, and silently keeping the extra bits is what produced a distance of 64
        // on an 8-bit hash and a similarity of -7.
        if ((parsed & ~MaskFor(bitLength)) != 0)
        {
            return false;
        }

        hash = new ImageHash(parsed, bitLength);
        return true;
    }

    /// <summary>
    /// Returns the hash as lowercase hexadecimal, padded to <see cref="BitLength"/>.
    /// </summary>
    public override string ToString()
    {
        if (IsEmpty)
        {
            return string.Empty;
        }

        // Formatted at full width, then trimmed to the digits this hash declares.
        // Composing an "x{n}" format string allocated one on every call.
        Span<char> buffer = stackalloc char[MaxHexDigits];
        Value.TryFormat(buffer, out _, "x16", CultureInfo.InvariantCulture);

        return new string(buffer[(MaxHexDigits - HexDigitsFor(BitLength))..]);
    }

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is ImageHash other && Equals(other);

    /// <inheritdoc />
    public bool Equals(ImageHash other) => Value == other.Value && BitLength == other.BitLength;

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(Value, BitLength);

    /// <summary>
    /// Compares two hashes for value equality.
    /// </summary>
    public static bool operator ==(ImageHash left, ImageHash right) => left.Equals(right);

    /// <summary>
    /// Compares two hashes for value inequality.
    /// </summary>
    public static bool operator !=(ImageHash left, ImageHash right) => !left.Equals(right);

    private static ulong MaskFor(int bitLength) =>
        bitLength == MaxBits ? ulong.MaxValue : (1UL << bitLength) - 1;

    private static int HexDigitsFor(int bitLength) => (bitLength + 3) / 4;

    private static void ThrowIfEmpty(ImageHash hash)
    {
        if (hash.IsEmpty)
        {
            throw new InvalidOperationException(
                "This is default(ImageHash), not a computed hash. "
                + "Use ImageHasher.Compute or ImageHash.Parse.");
        }
    }
}
