namespace CaptchaGridAnalyzer.Exceptions;

/// <summary>
/// Exception thrown when an image format is invalid or unsupported.
/// </summary>
public class InvalidImageFormatException : CaptchaAnalysisException
{
    /// <summary>
    /// Gets the unsupported image format.
    /// </summary>
    public string Format { get; }

    /// <summary>
    /// Initializes a new instance of the InvalidImageFormatException class.
    /// </summary>
    /// <param name="format">The invalid or unsupported image format.</param>
    public InvalidImageFormatException(string format)
        : base($"Invalid or unsupported image format: {format}")
    {
        Format = format;
    }
}
