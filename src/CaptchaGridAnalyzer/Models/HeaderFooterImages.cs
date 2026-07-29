namespace CaptchaGridAnalyzer.Models;

/// <summary>
/// Represents file paths for extracted header and footer region images.
/// </summary>
public class HeaderFooterImages
{
    /// <summary>
    /// Gets or sets the relative file path of the saved header image (null if no header).
    /// </summary>
    public string? HeaderFilePath { get; set; }

    /// <summary>
    /// Gets or sets the relative file path of the saved footer image (null if no footer).
    /// </summary>
    public string? FooterFilePath { get; set; }

    /// <summary>
    /// Gets or sets the text recognized (via OCR) in the header image, e.g. the
    /// CAPTCHA's instruction ("Select all squares with traffic lights"). Null if
    /// there's no header or no text was recognized.
    /// </summary>
    public string? HeaderText { get; set; }
}
