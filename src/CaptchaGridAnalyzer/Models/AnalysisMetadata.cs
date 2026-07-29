namespace CaptchaGridAnalyzer.Models;

/// <summary>
/// Represents metadata about the CAPTCHA analysis process.
/// </summary>
public record AnalysisMetadata
{
    /// <summary>
    /// Gets the timestamp when the analysis was performed.
    /// </summary>
    public DateTime Timestamp { get; init; }

    /// <summary>
    /// Gets the size of the original image.
    /// </summary>
    public ImageSize ImageSize { get; init; }

    /// <summary>
    /// Gets the output folder path where quadrants were saved.
    /// </summary>
    public string OutputFolder { get; init; }

    /// <summary>
    /// Gets the total processing time for the analysis.
    /// </summary>
    public TimeSpan ProcessingTime { get; init; }

    /// <summary>
    /// Initializes a new instance of the AnalysisMetadata record.
    /// </summary>
    /// <param name="timestamp">The timestamp of the analysis.</param>
    /// <param name="imageSize">The size of the original image.</param>
    /// <param name="outputFolder">The output folder path.</param>
    /// <param name="processingTime">The processing time duration.</param>
    public AnalysisMetadata(DateTime timestamp, ImageSize imageSize, string outputFolder, TimeSpan processingTime)
    {
        Timestamp = timestamp;
        ImageSize = imageSize;
        OutputFolder = outputFolder;
        ProcessingTime = processingTime;
    }
}
