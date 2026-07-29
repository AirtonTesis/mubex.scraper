namespace CaptchaGridAnalyzer.Exceptions;

/// <summary>
/// Exception thrown when a required file is not found.
/// </summary>
public class FileNotFoundException : CaptchaAnalysisException
{
    /// <summary>
    /// Gets the path of the file that was not found.
    /// </summary>
    public string FilePath { get; }

    /// <summary>
    /// Initializes a new instance of the FileNotFoundException class.
    /// </summary>
    /// <param name="filePath">The path of the file that was not found.</param>
    public FileNotFoundException(string filePath)
        : base($"File not found: {filePath}")
    {
        FilePath = filePath;
    }
}
