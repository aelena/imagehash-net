namespace NetImgHash;

/// <summary>
/// Supported perceptual hash algorithms.
/// </summary>
public enum HashAlgorithm
{
    /// <summary>
    /// Computes the average hash (aHash), suited to fast duplicate detection.
    /// </summary>
    AverageHash = 0,

    /// <summary>
    /// Computes the difference hash (dHash), suited to minor edit detection.
    /// </summary>
    DifferenceHash = 1,

    /// <summary>
    /// Computes the DCT-based perceptual hash (pHash), suited to detecting re-compressed,
    /// blurred or colour-adjusted copies. Bit-compatible with Python <c>imagehash.phash</c>.
    /// </summary>
    PerceptualHash = 2,

    /// <summary>
    /// Reserves the wavelet hash (wHash) algorithm for a later release.
    /// </summary>
    WaveletHash = 3
}
