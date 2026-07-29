namespace CaptchaGridAnalyzer.Models;

/// <summary>
/// Represents the dimensions of a grid structure.
/// </summary>
public record GridSize
{
    /// <summary>
    /// Gets the number of rows in the grid.
    /// </summary>
    public int Rows { get; init; }

    /// <summary>
    /// Gets the number of columns in the grid.
    /// </summary>
    public int Columns { get; init; }

    /// <summary>
    /// Gets the total number of cells in the grid (Rows * Columns).
    /// </summary>
    public int TotalCells => Rows * Columns;

    /// <summary>
    /// Initializes a new instance of the GridSize record.
    /// </summary>
    /// <param name="rows">The number of rows in the grid.</param>
    /// <param name="columns">The number of columns in the grid.</param>
    public GridSize(int rows, int columns)
    {
        Rows = rows;
        Columns = columns;
    }
}
