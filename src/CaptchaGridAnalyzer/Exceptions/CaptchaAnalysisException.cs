namespace CaptchaGridAnalyzer.Exceptions;

/// <summary>
/// Base exception for all CAPTCHA analysis related errors.
/// </summary>
public class CaptchaAnalysisException : Exception
{
    /// <summary>
    /// Initializes a new instance of the CaptchaAnalysisException class.
    /// </summary>
    /// <param name="message">The error message.</param>
    public CaptchaAnalysisException(string message) : base(message) { }

    /// <summary>
    /// Initializes a new instance of the CaptchaAnalysisException class with an inner exception.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception.</param>
    public CaptchaAnalysisException(string message, Exception innerException)
        : base(message, innerException) { }
}
