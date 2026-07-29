namespace CaptchaGridAnalyzer.Exceptions;

/// <summary>
/// Exception thrown when quadrant extraction bounds are exceeded.
/// </summary>
public class ExtractionBoundsException : CaptchaAnalysisException
{
    /// <summary>
    /// Gets the row where the bounds were exceeded.
    /// </summary>
    public int Row { get; }

    /// <summary>
    /// Gets the column where the bounds were exceeded.
    /// </summary>
    public int Column { get; }

    /// <summary>
    /// Initializes a new instance of the ExtractionBoundsException class.
    /// </summary>
    /// <param name="row">The row where bounds were exceeded.</param>
    /// <param name="column">The column where bounds were exceeded.</param>
    public ExtractionBoundsException(int row, int column)
        : base($"Quadrant extraction bounds exceeded at row {row}, column {column}")
    {
        Row = row;
        Column = column;
    }
}
