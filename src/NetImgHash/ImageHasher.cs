using NetImgHash.Internal;

namespace NetImgHash;

/// <summary>
/// Entry point for perceptual image hashing operations.
/// </summary>
public static class ImageHasher
{
    /// <summary>
    /// Computes a perceptual hash for the supplied image stream.
    /// </summary>
    public static ImageHash Compute(Stream stream, HashAlgorithm algorithm)
    {
        ArgumentNullException.ThrowIfNull(stream);

        return algorithm switch
        {
            HashAlgorithm.AverageHash => AverageHashAlgorithm.Compute(stream),
            HashAlgorithm.DifferenceHash => DifferenceHashAlgorithm.Compute(stream),
            HashAlgorithm.PerceptualHash => throw new NotSupportedException("PerceptualHash will be added in a follow-up release."),
            HashAlgorithm.WaveletHash => throw new NotSupportedException("WaveletHash will be added in a future release."),
            _ => throw new ArgumentOutOfRangeException(nameof(algorithm), algorithm, "Unsupported hash algorithm.")
        };
    }

    /// <summary>
    /// Computes a perceptual hash for the image stored at the supplied file path.
    /// </summary>
    public static ImageHash Compute(string path, HashAlgorithm algorithm)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        using var stream = File.OpenRead(path);
        return Compute(stream, algorithm);
    }
}
