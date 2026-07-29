namespace CaptchaGridAnalyzer.Models;

/// <summary>
/// Represents the dimensions of an image.
/// </summary>
public record ImageSize
{
    /// <summary>
    /// Gets the width of the image in pixels.
    /// </summary>
    public int Width { get; init; }

    /// <summary>
    /// Gets the height of the image in pixels.
    /// </summary>
    public int Height { get; init; }

    /// <summary>
    /// Initializes a new instance of the ImageSize record.
    /// </summary>
    /// <param name="width">The width of the image in pixels.</param>
    /// <param name="height">The height of the image in pixels.</param>
    public ImageSize(int width, int height)
    {
        Width = width;
        Height = height;
    }
}
