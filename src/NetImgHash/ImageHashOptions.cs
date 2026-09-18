namespace NetImgHash;

/// <summary>
/// Limits applied while decoding an image for hashing.
/// </summary>
/// <remarks>
/// A perceptual hash is often computed on files a stranger uploaded. The dimensions in
/// an image header are trusted by every decoder, so a 70-byte PNG that claims to be
/// 20000×20000 costs over two gigabytes and many seconds to decode before a single
/// hash bit is produced. <see cref="MaxPixels"/> is checked against the header before
/// any pixel buffer is allocated.
/// </remarks>
public sealed class ImageHashOptions
{
    /// <summary>
    /// The default pixel budget: 50 megapixels, comfortably above any phone camera and
    /// most DSLRs, while capping the decode buffer at roughly 200 MB.
    /// </summary>
    public const long DefaultMaxPixels = 50_000_000;

    /// <summary>
    /// Gets the options used when none are supplied.
    /// </summary>
    public static ImageHashOptions Default { get; } = new();

    /// <summary>
    /// Gets the largest image, in pixels (width × height), that will be decoded.
    /// An image above this is rejected with <see cref="ImageTooLargeException"/>
    /// after reading only its header.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">When set to zero or a negative value.</exception>
    public long MaxPixels
    {
        get;
        init
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
            field = value;
        }
    } = DefaultMaxPixels;
}
