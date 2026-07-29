namespace CaptchaGridAnalyzer.Exceptions;

/// <summary>
/// Exception thrown when an invalid grid structure is detected.
/// </summary>
public class InvalidGridStructureException : CaptchaAnalysisException
{
    /// <summary>
    /// Gets the number of rows detected in the grid.
    /// </summary>
    public int DetectedRows { get; }

    /// <summary>
    /// Gets the number of columns detected in the grid.
    /// </summary>
    public int DetectedColumns { get; }

    /// <summary>
    /// Initializes a new instance of the InvalidGridStructureException class.
    /// </summary>
    /// <param name="rows">The number of rows detected.</param>
    /// <param name="columns">The number of columns detected.</param>
    public InvalidGridStructureException(int rows, int columns)
        : base($"Invalid grid structure detected: {rows}x{columns}")
    {
        DetectedRows = rows;
        DetectedColumns = columns;
    }
}
