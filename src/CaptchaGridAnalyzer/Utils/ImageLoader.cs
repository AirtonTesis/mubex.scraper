using CaptchaGridAnalyzer.Exceptions;
using OpenCvSharp;

namespace CaptchaGridAnalyzer.Utils;

/// <summary>
/// Handles loading and validating CAPTCHA images.
/// </summary>
public class ImageLoader
{
    private static readonly HashSet<string> SupportedFormats = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".bmp", ".webp"
    };

    /// <summary>
    /// Loads an image from the specified path.
    /// </summary>
    /// <param name="imagePath">The path to the image file.</param>
    /// <returns>The loaded image as a Mat object.</returns>
    /// <exception cref="FileNotFoundException">Thrown when the file does not exist.</exception>
    /// <exception cref="InvalidImageFormatException">Thrown when the file format is not supported.</exception>
    /// <exception cref="ImageLoadException">Thrown when the image cannot be read.</exception>
    public Mat Load(string imagePath)
    {
        // Check file existence
        if (!File.Exists(imagePath))
            throw new Exceptions.FileNotFoundException(imagePath);

        // Validate format
        string extension = Path.GetExtension(imagePath);
        if (string.IsNullOrEmpty(extension) || !SupportedFormats.Contains(extension))
            throw new InvalidImageFormatException(extension);

        try
        {
            // Load image using OpenCV
            Mat image = Cv2.ImRead(imagePath, ImreadModes.Color);

            if (image.Empty())
            {
                image.Dispose();
                throw new ImageLoadException(
                    "Image file exists but contains no valid image data",
                    new InvalidOperationException("Empty image"));
            }

            return image;
        }
        catch (Exception ex) when (ex is not CaptchaAnalysisException)
        {
            throw new ImageLoadException($"Error reading image file: {ex.Message}", ex);
        }
    }
}
