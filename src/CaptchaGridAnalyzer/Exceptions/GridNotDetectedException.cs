namespace CaptchaGridAnalyzer.Exceptions;

/// <summary>
/// Exception thrown when no grid structure is detected in the image.
/// </summary>
public class GridNotDetectedException : CaptchaAnalysisException
{
    /// <summary>
    /// Initializes a new instance of the GridNotDetectedException class.
    /// </summary>
    public GridNotDetectedException()
        : base("No grid structure detected in the image") { }

    /// <summary>
    /// Initializes a new instance of the GridNotDetectedException class with a reason.
    /// </summary>
    /// <param name="reason">The reason why the grid was not detected.</param>
    public GridNotDetectedException(string reason)
        : base($"Grid not detected: {reason}") { }
}
