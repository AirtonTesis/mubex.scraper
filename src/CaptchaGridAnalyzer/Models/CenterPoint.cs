namespace CaptchaGridAnalyzer.Models;

/// <summary>
/// Represents a pixel coordinate.
/// </summary>
public record CenterPoint
{
    /// <summary>
    /// Gets the X coordinate in pixels.
    /// </summary>
    public int X { get; init; }

    /// <summary>
    /// Gets the Y coordinate in pixels.
    /// </summary>
    public int Y { get; init; }

    /// <summary>
    /// Initializes a new instance of the CenterPoint record.
    /// </summary>
    /// <param name="x">The X coordinate in pixels.</param>
    /// <param name="y">The Y coordinate in pixels.</param>
    public CenterPoint(int x, int y)
    {
        X = x;
        Y = y;
    }
}
