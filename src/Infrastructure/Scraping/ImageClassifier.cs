using Microsoft.Extensions.Logging;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace Infrastructure.Scraping;

public interface IImageClassifier
{
    Task<List<ImageClassification>> ClassifyAsync(byte[] imageBytes, CancellationToken cancellationToken = default);
    Task<List<ImageClassification>> ClassifyAsync(string imagePath, CancellationToken cancellationToken = default);
}

public class ImageClassification
{
    public int Index { get; set; }
    public string Label { get; set; } = string.Empty;
    public float Confidence { get; set; }
}

public class MobileNetClassifier : IImageClassifier, IDisposable
{
    private readonly ILogger<MobileNetClassifier> _logger;
    private readonly InferenceSession _session;
    private readonly string[] _labels;
    private readonly string _inputName;
    private readonly SemaphoreSlim _inferenceLock = new(1, 1);

    public MobileNetClassifier(ILogger<MobileNetClassifier> logger)
    {
        _logger = logger;
        var modelPath = Path.Combine(AppContext.BaseDirectory, "models", "mobilenetv3-small.onnx");
        var labelsPath = Path.Combine(AppContext.BaseDirectory, "models", "imagenet_labels.txt");
        
        if (!File.Exists(modelPath))
            throw new FileNotFoundException($"Model not found: {modelPath}");
        if (!File.Exists(labelsPath))
            throw new FileNotFoundException($"Labels not found: {labelsPath}");
        
        _session = new InferenceSession(modelPath);
        // Skip first "background" label - MobileNetV3 outputs 1000 classes (indices 0-999)
        // The labels file has "background" at index 0, actual classes at indices 1-1000
        _labels = File.ReadAllLines(labelsPath)
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .Skip(1) // Skip "background" entry
            .ToArray();
        _inputName = _session.InputMetadata.Keys.First();
        
        _logger.LogInformation("MobileNet loaded - Input: {Input}, Labels: {Labels}",
            _inputName, _labels.Length);
    }

    public Task<List<ImageClassification>> ClassifyAsync(string imagePath, CancellationToken cancellationToken = default)
    {
        var imageBytes = File.ReadAllBytes(imagePath);
        return ClassifyAsync(imageBytes, cancellationToken);
    }

    public async Task<List<ImageClassification>> ClassifyAsync(byte[] imageBytes, CancellationToken cancellationToken = default)
    {
        using var ms = new MemoryStream(imageBytes);
        using var image = System.Drawing.Image.FromStream(ms);
        using var resized = new System.Drawing.Bitmap(image, new System.Drawing.Size(224, 224));
        
        var input = new DenseTensor<float>(new[] { 1, 3, 224, 224 });
        for (int y = 0; y < 224; y++)
        {
            for (int x = 0; x < 224; x++)
            {
                var pixel = resized.GetPixel(x, y);
                input[0, 0, y, x] = (pixel.R / 255.0f - 0.485f) / 0.229f;
                input[0, 1, y, x] = (pixel.G / 255.0f - 0.456f) / 0.224f;
                input[0, 2, y, x] = (pixel.B / 255.0f - 0.406f) / 0.225f;
            }
        }
        
        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor(_inputName, input)
        };
        
        float[] output;
        await _inferenceLock.WaitAsync(cancellationToken);
        try
        {
            using var results = _session.Run(inputs);
            output = results.First().AsEnumerable<float>().ToArray();
        }
        finally
        {
            _inferenceLock.Release();
        }
        
        var softmax = Softmax(output);
        var predictions = softmax
            .Select((score, index) => new ImageClassification
            {
                Index = index,
                Label = index < _labels.Length ? _labels[index] : $"class_{index}",
                Confidence = score
            })
            .OrderByDescending(p => p.Confidence)
            .Take(5)
            .ToList();
        
        return predictions;
    }

    private static float[] Softmax(float[] logits)
    {
        var max = logits.Max();
        var exps = logits.Select(x => (float)Math.Exp(x - max)).ToArray();
        var sum = exps.Sum();
        return exps.Select(x => x / sum).ToArray();
    }

    public void Dispose()
    {
        _session?.Dispose();
        _inferenceLock?.Dispose();
    }
}
