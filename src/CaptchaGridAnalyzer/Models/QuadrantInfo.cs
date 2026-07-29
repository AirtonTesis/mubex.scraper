namespace CaptchaGridAnalyzer.Models;

/// <summary>
/// Represents information about an extracted quadrant from a CAPTCHA grid.
/// </summary>
public record QuadrantInfo
{
    /// <summary>
    /// Gets the sequential index of the quadrant (zero-based).
    /// </summary>
    public int Index { get; init; }

    /// <summary>
    /// Gets the row position of the quadrant in the grid (zero-based).
    /// </summary>
    public int Row { get; init; }

    /// <summary>
    /// Gets the column position of the quadrant in the grid (zero-based).
    /// </summary>
    public int Column { get; init; }

    /// <summary>
    /// Gets the X coordinate of the top-left corner of the quadrant.
    /// </summary>
    public int X { get; init; }

    /// <summary>
    /// Gets the Y coordinate of the top-left corner of the quadrant.
    /// </summary>
    public int Y { get; init; }

    /// <summary>
    /// Gets the width of the quadrant in pixels.
    /// </summary>
    public int Width { get; init; }

    /// <summary>
    /// Gets the height of the quadrant in pixels.
    /// </summary>
    public int Height { get; init; }

    /// <summary>
    /// Gets the relative file path of the saved original quadrant image.
    /// </summary>
    public string FilePath { get; init; }

    /// <summary>
    /// Gets the center point of the quadrant, in coordinates relative to the full
    /// source image (not the quadrant's own local width/height).
    /// </summary>
    public CenterPoint Center => new CenterPoint(X + Width / 2, Y + Height / 2);

    /// <summary>
    /// Initializes a new instance of the QuadrantInfo record.
    /// </summary>
    /// <param name="index">The sequential index of the quadrant.</param>
    /// <param name="row">The row position in the grid.</param>
    /// <param name="column">The column position in the grid.</param>
    /// <param name="x">The X coordinate of the top-left corner.</param>
    /// <param name="y">The Y coordinate of the top-left corner.</param>
    /// <param name="width">The width in pixels.</param>
    /// <param name="height">The height in pixels.</param>
    /// <param name="filePath">The relative file path of the original image.</param>
    public QuadrantInfo(int index, int row, int column, int x, int y, int width, int height, string filePath)
    {
        Index = index;
        Row = row;
        Column = column;
        X = x;
        Y = y;
        Width = width;
        Height = height;
        FilePath = filePath;
    }
}
