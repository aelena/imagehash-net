using NetImgHash.Internal;

namespace NetImgHash;

/// <summary>
/// Entry point for perceptual image hashing operations.
/// </summary>
public static class ImageHasher
{
    /// <summary>
    /// Computes a perceptual hash for the supplied image stream, using
    /// <see cref="ImageHashOptions.Default"/>.
    /// </summary>
    /// <remarks>
    /// The stream is read from its current position and is not disposed. Only the first
    /// frame of an animated or multi-page image is decoded.
    /// </remarks>
    /// <exception cref="ImageTooLargeException">
    /// When the image header declares more than <see cref="ImageHashOptions.MaxPixels"/> pixels.
    /// </exception>
    /// <exception cref="NotSupportedException">
    /// When <paramref name="algorithm"/> is reserved but not yet implemented.
    /// </exception>
    public static ImageHash Compute(Stream stream, HashAlgorithm algorithm) =>
        Compute(stream, algorithm, ImageHashOptions.Default);

    /// <summary>
    /// Computes a perceptual hash for the supplied image stream.
    /// </summary>
    /// <inheritdoc cref="Compute(Stream, HashAlgorithm)"/>
    public static ImageHash Compute(Stream stream, HashAlgorithm algorithm, ImageHashOptions options)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(options);

        return algorithm switch
        {
            HashAlgorithm.AverageHash => AverageHashAlgorithm.Compute(stream, options),
            HashAlgorithm.DifferenceHash => DifferenceHashAlgorithm.Compute(stream, options),
            HashAlgorithm.PerceptualHash => PerceptualHashAlgorithm.Compute(stream, options),
            HashAlgorithm.WaveletHash => throw new NotSupportedException("WaveletHash will be added in a future release."),
            _ => throw new ArgumentOutOfRangeException(nameof(algorithm), algorithm, "Unsupported hash algorithm.")
        };
    }

    /// <summary>
    /// Computes a perceptual hash for the image stored at the supplied file path, using
    /// <see cref="ImageHashOptions.Default"/>.
    /// </summary>
    /// <inheritdoc cref="Compute(Stream, HashAlgorithm)"/>
    public static ImageHash Compute(string path, HashAlgorithm algorithm) =>
        Compute(path, algorithm, ImageHashOptions.Default);

    /// <summary>
    /// Computes a perceptual hash for the image stored at the supplied file path.
    /// </summary>
    /// <inheritdoc cref="Compute(Stream, HashAlgorithm)"/>
    public static ImageHash Compute(string path, HashAlgorithm algorithm, ImageHashOptions options)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(options);

        using var stream = File.OpenRead(path);
        return Compute(stream, algorithm, options);
    }
}
