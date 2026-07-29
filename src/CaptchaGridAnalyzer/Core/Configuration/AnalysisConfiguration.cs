namespace CaptchaGridAnalyzer.Core.Configuration;

/// <summary>
/// Configuration for the CAPTCHA analysis pipeline.
/// </summary>
public class AnalysisConfiguration
{
    /// <summary>
    /// Minimum grid size (rows/columns).
    /// </summary>
    public int MinGridSize { get; set; } = 2;

    /// <summary>
    /// Maximum grid size (rows/columns).
    /// </summary>
    public int MaxGridSize { get; set; } = 10;

    /// <summary>
    /// Validates the configuration parameters.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when configuration values are invalid.</exception>
    public void Validate()
    {
        if (MinGridSize < 2 || MaxGridSize > 10 || MinGridSize > MaxGridSize)
            throw new ArgumentException(
                $"Invalid grid size configuration: Min={MinGridSize}, Max={MaxGridSize}. Must be between 2 and 10 with Min <= Max.");
    }
}
