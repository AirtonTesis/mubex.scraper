namespace CaptchaGridAnalyzer.Models;

/// <summary>
/// Represents the complete result of a CAPTCHA grid analysis.
/// </summary>
public record CaptchaAnalysisResult
{
    /// <summary>
    /// Gets the metadata about the analysis process.
    /// </summary>
    public AnalysisMetadata Metadata { get; init; }

    /// <summary>
    /// Gets the detected grid dimensions.
    /// </summary>
    public GridSize Grid { get; init; }

    /// <summary>
    /// Gets the list of extracted quadrants.
    /// </summary>
    public List<QuadrantInfo> Quadrants { get; init; }

    /// <summary>
    /// Gets the header height in pixels (above grid content).
    /// </summary>
    public int HeaderHeight { get; init; }

    /// <summary>
    /// Gets the footer height in pixels (below grid content).
    /// </summary>
    public int FooterHeight { get; init; }

    /// <summary>
    /// Gets the Y coordinate where the footer starts (after last separator).
    /// </summary>
    public int FooterStartY { get; init; }

    /// <summary>
    /// Gets the relative file path of the saved header image.
    /// </summary>
    public string? HeaderFilePath { get; init; }

    /// <summary>
    /// Gets the relative file path of the saved footer image.
    /// </summary>
    public string? FooterFilePath { get; init; }

    /// <summary>
    /// Gets the text recognized (via OCR) in the header image, e.g. the CAPTCHA's
    /// instruction ("Select all squares with traffic lights").
    /// </summary>
    public string? HeaderText { get; init; }

    /// <summary>
    /// Initializes a new instance of the CaptchaAnalysisResult record.
    /// </summary>
    /// <param name="metadata">The analysis metadata.</param>
    /// <param name="grid">The detected grid size.</param>
    /// <param name="quadrants">The list of extracted quadrants.</param>
    /// <param name="headerHeight">Header height in pixels.</param>
    /// <param name="footerHeight">Footer height in pixels.</param>
    /// <param name="headerFilePath">Relative file path of the header image.</param>
    /// <param name="footerFilePath">Relative file path of the footer image.</param>
    /// <param name="headerText">Text recognized (via OCR) in the header image.</param>
    public CaptchaAnalysisResult(AnalysisMetadata metadata, GridSize grid, List<QuadrantInfo> quadrants,
        int headerHeight = 0, int footerHeight = 0, int footerStartY = 0,
        string? headerFilePath = null, string? footerFilePath = null, string? headerText = null)
    {
        Metadata = metadata;
        Grid = grid;
        Quadrants = quadrants;
        HeaderHeight = headerHeight;
        FooterHeight = footerHeight;
        FooterStartY = footerStartY;
        HeaderFilePath = headerFilePath;
        FooterFilePath = footerFilePath;
        HeaderText = headerText;
    }
}
