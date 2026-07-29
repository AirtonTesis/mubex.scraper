using OpenCvSharp;
using Tesseract;

namespace CaptchaGridAnalyzer.Ocr;

/// <summary>
/// Extracts text from an image region using Tesseract OCR.
/// </summary>
public class TextExtractor : IDisposable
{
    private readonly TesseractEngine _engine;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the TextExtractor class.
    /// </summary>
    /// <param name="tessdataPath">Directory containing the .traineddata language files. Defaults to a "tessdata" folder next to the running executable.</param>
    /// <param name="languages">Tesseract language code(s), e.g. "eng" or "eng+por".</param>
    public TextExtractor(string? tessdataPath = null, string languages = "eng+por")
    {
        tessdataPath ??= Path.Combine(AppContext.BaseDirectory, "tessdata");
        _engine = new TesseractEngine(tessdataPath, languages, EngineMode.Default);
    }

    /// <summary>
    /// Recognizes and returns the text found in the given image, trimmed of
    /// surrounding whitespace. Returns an empty string if the image is empty or
    /// no text is recognized.
    /// </summary>
    /// <param name="image">The image region to run OCR on (e.g. a header banner).</param>
    public string ExtractText(Mat image)
    {
        if (image.Empty()) return string.Empty;

        // Tesseract's own color handling can fail outright (returning nothing) on
        // some colored banners it otherwise has no trouble with once converted to
        // grayscale first - this made the difference between empty and a full,
        // confident read on several real captcha samples.
        using var gray = new Mat();
        if (image.Channels() == 1)
        {
            image.CopyTo(gray);
        }
        else
        {
            Cv2.CvtColor(image, gray, ColorConversionCodes.BGR2GRAY);
        }

        Cv2.ImEncode(".png", gray, out byte[] pngBytes);
        using var pix = Pix.LoadFromMemory(pngBytes);
        using var page = _engine.Process(pix);
        return page.GetText().Trim();
    }

    /// <summary>
    /// Disposes of the underlying Tesseract engine.
    /// </summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            _engine.Dispose();
            _disposed = true;
        }
        GC.SuppressFinalize(this);
    }
}
