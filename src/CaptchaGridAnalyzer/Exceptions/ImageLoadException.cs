namespace CaptchaGridAnalyzer.Exceptions;

/// <summary>
/// Exception thrown when an image fails to load.
/// </summary>
public class ImageLoadException : CaptchaAnalysisException
{
    /// <summary>
    /// Initializes a new instance of the ImageLoadException class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception that caused the failure.</param>
    public ImageLoadException(string message, Exception innerException)
        : base($"Failed to load image: {message}", innerException) { }
}
