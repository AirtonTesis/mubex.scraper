using OpenCvSharp;

namespace CaptchaGridAnalyzer.Extraction;

/// <summary>
/// Handles file system operations for saving quadrant images.
/// </summary>
public class FileManager
{
    /// <summary>
    /// Creates a directory if it doesn't exist.
    /// </summary>
    /// <param name="path">The directory path to create.</param>
    public void CreateDirectory(string path)
    {
        if (!Directory.Exists(path))
            Directory.CreateDirectory(path);
    }

    /// <summary>
    /// Clears all files from a directory.
    /// </summary>
    /// <param name="path">The directory path to clear.</param>
    public void ClearDirectory(string path)
    {
        if (!Directory.Exists(path)) return;

        var files = Directory.GetFiles(path);
        foreach (var file in files)
        {
            File.Delete(file);
        }
    }

    /// <summary>
    /// Checks if a directory exists.
    /// </summary>
    /// <param name="path">The directory path to check.</param>
    /// <returns>True if the directory exists; otherwise false.</returns>
    public bool DirectoryExists(string path) => Directory.Exists(path);

    /// <summary>
    /// Saves a Mat image as PNG (lossless) for maximum quality.
    /// </summary>
    /// <param name="image">The image to save.</param>
    /// <param name="filePath">The path where to save the image.</param>
    public void SaveImageAsPng(Mat image, string filePath)
    {
        var parameters = new[]
        {
            new ImageEncodingParam(ImwriteFlags.PngCompression, 0)
        };

        Cv2.ImWrite(filePath, image, parameters);
    }

    /// <summary>
    /// Gets the filename without extension from a file path.
    /// </summary>
    /// <param name="filePath">The file path.</param>
    /// <returns>The filename without extension.</returns>
    public string GetFileNameWithoutExtension(string filePath)
    {
        return Path.GetFileNameWithoutExtension(filePath);
    }

    /// <summary>
    /// Combines multiple path segments into one path.
    /// </summary>
    /// <param name="paths">The path segments to combine.</param>
    /// <returns>The combined path.</returns>
    public string CombinePath(params string[] paths)
    {
        return Path.Combine(paths);
    }
}
