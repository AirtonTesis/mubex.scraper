using CaptchaGridAnalyzer.Core.Configuration;
using CaptchaGridAnalyzer.Detection;
using CaptchaGridAnalyzer.Exceptions;
using CaptchaGridAnalyzer.Extraction;
using CaptchaGridAnalyzer.Models;
using CaptchaGridAnalyzer.Ocr;
using CaptchaGridAnalyzer.Output;
using CaptchaGridAnalyzer.Utils;
using OpenCvSharp;
using System.Diagnostics;

namespace CaptchaGridAnalyzer.Core;

/// <summary>
/// Main orchestrator that coordinates the CAPTCHA analysis pipeline.
/// </summary>
public class CaptchaAnalyzer : IDisposable
{
    private readonly AnalysisConfiguration _config;
    private readonly ImageLoader _imageLoader;
    private readonly GridDetector _gridDetector;
    private readonly QuadrantExtractor _quadrantExtractor;
    private readonly JsonGenerator _jsonGenerator;
    private readonly FileManager _fileManager;
    private readonly TextExtractor? _textExtractor;

    private Mat? _currentImage;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the CaptchaAnalyzer class.
    /// </summary>
    /// <param name="config">Optional configuration. Uses default if null.</param>
    public CaptchaAnalyzer(AnalysisConfiguration? config = null)
    {
        _config = config ?? new AnalysisConfiguration();
        _config.Validate();

        // Initialize utilities
        _fileManager = new FileManager();
        _imageLoader = new ImageLoader();

        // Initialize detection components
        _gridDetector = new GridDetector(
            _config.MinGridSize,
            _config.MaxGridSize);

        // OCR is best-effort: if tessdata/the native engine isn't available for
        // some reason, skip header text recognition rather than fail the whole
        // analysis (grid/quadrant extraction doesn't depend on it).
        try
        {
            _textExtractor = new TextExtractor();
        }
        catch (Exception)
        {
            _textExtractor = null;
        }

        _quadrantExtractor = new QuadrantExtractor(_fileManager, _textExtractor);
        _jsonGenerator = new JsonGenerator();
    }

    /// <summary>
    /// Analyzes a CAPTCHA image and returns the results as JSON.
    /// </summary>
    /// <param name="imagePath">Path to the CAPTCHA image.</param>
    /// <param name="outputDirectory">Directory to save quadrant images.</param>
    /// <returns>JSON string with analysis results.</returns>
    public string Analyze(string imagePath, string outputDirectory)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Stage 1: Load and validate image
            _currentImage = LoadAndValidateImage(imagePath);

            // Stage 2: Detect grid structure (GridDetector handles header/footer/borders internally)
            var gridInfo = DetectGrid(_currentImage);
            var gridSize = new GridSize(gridInfo.Rows, gridInfo.Columns);

            // Stage 3: Extract quadrants AND header/footer images
            string sourceImageName = _fileManager.GetFileNameWithoutExtension(imagePath);
            var (quadrants, headerFooterImages) = ExtractQuadrants(_currentImage, gridInfo, outputDirectory, sourceImageName);

            // Stage 4: Generate JSON
            stopwatch.Stop();
            var metadata = new AnalysisMetadata(
                DateTime.UtcNow,
                new ImageSize(_currentImage.Width, _currentImage.Height),
                _fileManager.CombinePath(outputDirectory, sourceImageName),
                stopwatch.Elapsed);

            var result = new CaptchaAnalysisResult(metadata, gridSize, quadrants,
                headerHeight: gridInfo.HeaderHeight,
                footerHeight: gridInfo.FooterHeight,
                footerStartY: gridInfo.FooterStartY,
                headerFilePath: headerFooterImages.HeaderFilePath,
                footerFilePath: headerFooterImages.FooterFilePath,
                headerText: headerFooterImages.HeaderText);
            return GenerateJson(result);
        }
        catch (CaptchaAnalysisException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new CaptchaAnalysisException("Unexpected error during CAPTCHA analysis", ex);
        }
        finally
        {
            _currentImage?.Dispose();
            _currentImage = null;
        }
    }

    /// <summary>
    /// Asynchronously analyzes a CAPTCHA image and returns the results as JSON.
    /// </summary>
    /// <param name="imagePath">Path to the CAPTCHA image.</param>
    /// <param name="outputDirectory">Directory to save quadrant images.</param>
    /// <returns>JSON string with analysis results.</returns>
    public Task<string> AnalyzeAsync(string imagePath, string outputDirectory)
    {
        return Task.Run(() => Analyze(imagePath, outputDirectory));
    }

    /// <summary>
    /// Loads and validates the image from the specified path.
    /// </summary>
    /// <param name="imagePath">The image path.</param>
    /// <returns>The loaded image.</returns>
    private Mat LoadAndValidateImage(string imagePath)
    {
        return _imageLoader.Load(imagePath);
    }

    /// <summary>
    /// Detects the grid structure in the image, returning detailed separator info.
    /// </summary>
    /// <param name="image">The original image.</param>
    /// <returns>The detected grid info with separator positions.</returns>
    private GridInfo DetectGrid(Mat image)
    {
        return _gridDetector.DetectGrid(image);
    }

    /// <summary>
    /// Extracts quadrants AND header/footer images from the image.
    /// </summary>
    /// <param name="image">The cropped image.</param>
    /// <param name="gridInfo">The grid info with separator positions.</param>
    /// <param name="outputDirectory">The output directory.</param>
    /// <param name="sourceImageName">The source image name.</param>
    /// <returns>Tuple of quadrant list and header/footer image paths.</returns>
    private (List<QuadrantInfo> quadrants, HeaderFooterImages headerFooter) ExtractQuadrants(
        Mat image, GridInfo gridInfo, string outputDirectory, string sourceImageName)
    {
        return _quadrantExtractor.ExtractAndSave(image, gridInfo, outputDirectory, sourceImageName);
    }

    /// <summary>
    /// Generates JSON from the analysis result.
    /// </summary>
    /// <param name="result">The analysis result.</param>
    /// <returns>JSON string.</returns>
    private string GenerateJson(CaptchaAnalysisResult result)
    {
        return _jsonGenerator.Generate(result);
    }

    /// <summary>
    /// Disposes of resources.
    /// </summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            _currentImage?.Dispose();
            _currentImage = null;
            _textExtractor?.Dispose();
            _disposed = true;
        }
        GC.SuppressFinalize(this);
    }
}
