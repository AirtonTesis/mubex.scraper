using CaptchaGridAnalyzer.Models;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CaptchaGridAnalyzer.Output;

/// <summary>
/// Generates structured JSON output from CAPTCHA analysis results.
/// </summary>
public class JsonGenerator
{
    private readonly JsonSerializerOptions _options;

    /// <summary>
    /// Initializes a new instance of the JsonGenerator class.
    /// </summary>
    public JsonGenerator()
    {
        _options = ConfigureOptions();
    }

    /// <summary>
    /// Generates a JSON string from the analysis result.
    /// </summary>
    /// <param name="result">The analysis result.</param>
    /// <returns>A formatted JSON string.</returns>
    public string Generate(CaptchaAnalysisResult result)
    {
        return JsonSerializer.Serialize(result, _options);
    }

    /// <summary>
    /// Saves the JSON output to a file.
    /// </summary>
    /// <param name="json">The JSON string to save.</param>
    /// <param name="filePath">The file path.</param>
    public void SaveToFile(string json, string filePath)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(filePath, json, System.Text.Encoding.UTF8);
    }

    /// <summary>
    /// Configures the JSON serializer options.
    /// </summary>
    /// <returns>Configured JsonSerializerOptions.</returns>
    private JsonSerializerOptions ConfigureOptions()
    {
        return new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
    }
}
