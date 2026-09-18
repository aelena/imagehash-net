using System.Globalization;

namespace NetImgHash;

/// <summary>
/// Thrown when an image's header declares more pixels than
/// <see cref="ImageHashOptions.MaxPixels"/> allows. No pixel data has been decoded
/// when this is raised.
/// </summary>
public sealed class ImageTooLargeException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ImageTooLargeException"/> class.
    /// </summary>
    public ImageTooLargeException()
        : this("The image exceeds the configured pixel budget.")
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ImageTooLargeException"/> class.
    /// </summary>
    public ImageTooLargeException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ImageTooLargeException"/> class.
    /// </summary>
    public ImageTooLargeException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ImageTooLargeException"/> class
    /// for an image of the given dimensions rejected against the given budget.
    /// </summary>
    public ImageTooLargeException(int width, int height, long maxPixels)
        : base(Describe(width, height, maxPixels))
    {
        Width = width;
        Height = height;
        MaxPixels = maxPixels;
    }

    /// <summary>Gets the width the image header declared.</summary>
    public int Width { get; }

    /// <summary>Gets the height the image header declared.</summary>
    public int Height { get; }

    /// <summary>Gets the pixel budget that was exceeded.</summary>
    public long MaxPixels { get; }

    private static string Describe(int width, int height, long maxPixels) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"The image is {width}×{height} ({(long)width * height:N0} pixels), above the budget of {maxPixels:N0}. Raise ImageHashOptions.MaxPixels if images this large are expected.");
}
