using CaptchaGridAnalyzer.Exceptions;
using CaptchaGridAnalyzer.Models;
using CaptchaGridAnalyzer.Ocr;
using OpenCvSharp;

namespace CaptchaGridAnalyzer.Extraction;

/// <summary>
/// Extracts individual quadrants from a CAPTCHA grid and saves original versions.
/// Also extracts header and footer regions as separate images.
/// Uses actual separator positions from GridInfo for accurate cell extraction.
/// </summary>
public class QuadrantExtractor
{
    private readonly FileManager _fileManager;
    private readonly TextExtractor? _textExtractor;

    /// <summary>
    /// Initializes a new instance of the QuadrantExtractor class.
    /// </summary>
    /// <param name="fileManager">The file manager instance.</param>
    /// <param name="textExtractor">Optional OCR reader for the header's instruction text. Skipped if null.</param>
    public QuadrantExtractor(FileManager fileManager, TextExtractor? textExtractor = null)
    {
        _fileManager = fileManager;
        _textExtractor = textExtractor;
    }

    /// <summary>
    /// Extracts and saves all quadrants from the image using actual separator positions.
    /// Also extracts and saves header and footer regions.
    /// </summary>
    /// <param name="image">The input image with grid.</param>
    /// <param name="gridInfo">The detected grid info with separator positions.</param>
    /// <param name="outputDirectory">Output directory path.</param>
    /// <param name="sourceImageName">The source image name (without extension).</param>
    /// <returns>Tuple of quadrant list and header/footer file paths.</returns>
    public (List<QuadrantInfo> quadrants, HeaderFooterImages headerFooter) ExtractAndSave(
        Mat image, GridInfo gridInfo, string outputDirectory, string sourceImageName)
    {
        string outputFolder = CreateOutputFolder(outputDirectory, sourceImageName);

        // Get actual cell boundaries from separator positions
        var rowBounds = gridInfo.GetRowBounds();
        var colBounds = gridInfo.GetColBounds();

        var quadrants = new List<QuadrantInfo>();
        int index = 0;

        for (int row = 0; row < gridInfo.Rows; row++)
        {
            for (int col = 0; col < gridInfo.Columns; col++)
            {
                int x = colBounds[col].Start;
                int y = rowBounds[row].Start;
                int width = colBounds[col].End - colBounds[col].Start;
                int height = rowBounds[row].End - rowBounds[row].Start;

                // Ensure positive dimensions
                if (width <= 0 || height <= 0)
                    continue;

                var bounds = new Rect(x, y, width, height);
                ValidateBounds(bounds, image.Size());

                // Extract the quadrant ROI
                using var quadrant = new Mat(image, bounds).Clone();

                // Save original quadrant as PNG (lossless)
                string originalFileName = $"quadrant_{row}_{col}.png";
                string originalFilePath = _fileManager.CombinePath(outputFolder, originalFileName);
                _fileManager.SaveImageAsPng(quadrant, originalFilePath);

                // Create relative path for JSON output
                string relativeOriginalPath = _fileManager.CombinePath(sourceImageName, originalFileName);

                quadrants.Add(new QuadrantInfo(
                    index: index,
                    row: row,
                    column: col,
                    x: x,
                    y: y,
                    width: width,
                    height: height,
                    filePath: relativeOriginalPath
                ));

                index++;
            }
        }

        // Extract and save header and footer regions
        var headerFooter = ExtractHeaderFooter(image, gridInfo, outputFolder, sourceImageName);

        return (quadrants, headerFooter);
    }

    /// <summary>
    /// Extracts and saves header and footer regions as separate images.
    /// </summary>
    private HeaderFooterImages ExtractHeaderFooter(
        Mat image, GridInfo gridInfo, string outputFolder, string sourceImageName)
    {
        var result = new HeaderFooterImages();
        // Span the widget's own width, not the full image - for a captcha
        // embedded in a larger screenshot, WidgetLeft/WidgetRight exclude the
        // surrounding page background (they equal 0/ImageWidth-1 otherwise).
        int widgetX = gridInfo.WidgetLeft;
        int widgetW = gridInfo.WidgetRight - gridInfo.WidgetLeft + 1;

        // Header: from y=WidgetTop to y=ContentTop
        if (gridInfo.HeaderHeight > 0)
        {
            using var header = new Mat(image, new Rect(widgetX, gridInfo.WidgetTop, widgetW, gridInfo.HeaderHeight)).Clone();
            string fileName = "header.png";
            string filePath = _fileManager.CombinePath(outputFolder, fileName);
            _fileManager.SaveImageAsPng(header, filePath);
            result.HeaderFilePath = _fileManager.CombinePath(sourceImageName, fileName);

            if (_textExtractor != null)
            {
                string text = _textExtractor.ExtractText(header);
                result.HeaderText = string.IsNullOrEmpty(text) ? null : text;
            }
        }

        // Footer: from FooterStartY (after last separator) to y=WidgetBottom
        if (gridInfo.FooterHeight > 0)
        {
            int footerY = gridInfo.FooterStartY;
            int footerH = gridInfo.FooterHeight;
            using var footer = new Mat(image, new Rect(widgetX, footerY, widgetW, footerH)).Clone();
            string fileName = "footer.png";
            string filePath = _fileManager.CombinePath(outputFolder, fileName);
            _fileManager.SaveImageAsPng(footer, filePath);
            result.FooterFilePath = _fileManager.CombinePath(sourceImageName, fileName);
        }

        return result;
    }

    /// <summary>
    /// Creates the output folder for quadrant images.
    /// </summary>
    /// <param name="outputDirectory">The base output directory.</param>
    /// <param name="sourceImageName">The source image name.</param>
    /// <returns>The created output folder path.</returns>
    private string CreateOutputFolder(string outputDirectory, string sourceImageName)
    {
        string outputFolder = _fileManager.CombinePath(outputDirectory, sourceImageName);

        if (_fileManager.DirectoryExists(outputFolder))
        {
            _fileManager.ClearDirectory(outputFolder);
        }
        else
        {
            _fileManager.CreateDirectory(outputFolder);
        }

        return outputFolder;
    }

    /// <summary>
    /// Validates that extraction bounds are within image dimensions.
    /// </summary>
    /// <param name="bounds">The extraction bounds.</param>
    /// <param name="imageSize">The image size.</param>
    /// <exception cref="ExtractionBoundsException">Thrown when bounds exceed image dimensions.</exception>
    private void ValidateBounds(Rect bounds, Size imageSize)
    {
        if (bounds.X + bounds.Width > imageSize.Width ||
            bounds.Y + bounds.Height > imageSize.Height ||
            bounds.X < 0 || bounds.Y < 0)
        {
            throw new ExtractionBoundsException(bounds.Y, bounds.X);
        }
    }
}
