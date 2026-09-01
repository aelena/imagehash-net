using System.Globalization;
using System.Numerics;

namespace NetImgHash;

/// <summary>
/// Represents a perceptual image hash value.
/// </summary>
public readonly struct ImageHash : IEquatable<ImageHash>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ImageHash"/> struct.
    /// </summary>
    /// <param name="value">The packed hash bits.</param>
    /// <param name="bitLength">The number of significant bits in <paramref name="value"/>.</param>
    public ImageHash(ulong value, int bitLength)
    {
        if (bitLength is <= 0 or > 64)
        {
            throw new ArgumentOutOfRangeException(nameof(bitLength), "Bit length must be between 1 and 64.");
        }

        Value = value;
        BitLength = bitLength;
    }

    /// <summary>
    /// Gets the packed hash bits.
    /// </summary>
    public ulong Value { get; }

    /// <summary>
    /// Gets the number of significant bits in <see cref="Value"/>.
    /// </summary>
    public int BitLength { get; }

    /// <summary>
    /// Computes the Hamming distance between this hash and another hash.
    /// </summary>
    public int HammingDistance(ImageHash other)
    {
        if (BitLength != other.BitLength)
        {
            throw new ArgumentException("Hash bit lengths must match.", nameof(other));
        }

        return BitOperations.PopCount(Value ^ other.Value);
    }

    /// <summary>
    /// Returns a similarity score between 0.0 and 1.0.
    /// </summary>
    public double Similarity(ImageHash other)
    {
        var distance = HammingDistance(other);
        return 1d - ((double)distance / BitLength);
    }

    /// <summary>
    /// Parses a hexadecimal hash string into an <see cref="ImageHash"/>.
    /// </summary>
    public static ImageHash Parse(string value, int bitLength = 64)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        if (!TryParse(value, bitLength, out var hash))
        {
            throw new FormatException("The supplied hash value was not a valid hexadecimal string.");
        }

        return hash;
    }

    /// <summary>
    /// Tries to parse a hexadecimal hash string into an <see cref="ImageHash"/>.
    /// </summary>
    public static bool TryParse(string? value, int bitLength, out ImageHash hash)
    {
        hash = default;

        if (string.IsNullOrWhiteSpace(value) || bitLength <= 0 || bitLength > 64)
        {
            return false;
        }

        var normalized = value.Trim();

        if (normalized.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[2..];
        }

        if (!ulong.TryParse(normalized, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var parsed))
        {
            return false;
        }

        hash = new ImageHash(parsed, bitLength);
        return true;
    }

    /// <inheritdoc />
    public override string ToString()
    {
        var hexDigits = (BitLength + 3) / 4;
        return Value.ToString($"x{hexDigits}", CultureInfo.InvariantCulture);
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
}
