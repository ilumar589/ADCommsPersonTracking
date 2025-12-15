using ADCommsPersonTracking.Api.Logging;
using ADCommsPersonTracking.Api.Models;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace ADCommsPersonTracking.Api.Services;

/// <summary>
/// Service for detecting clothing items using a fashion-trained YOLO ONNX model.
/// Uses ONNX Runtime for inference on cropped person images.
/// </summary>
public class ClothingDetectionService : IClothingDetectionService, IDisposable
{
    private readonly ILogger<ClothingDetectionService> _logger;
    private readonly IConfiguration _configuration;
    private InferenceSession? _session;
    private readonly string? _modelPath;
    private readonly bool _enabled;
    private readonly float _confidenceThreshold;
    private const int InputWidth = 640;
    private const int InputHeight = 640;
    private const float IouThreshold = 0.5f;
    
    // Fashion model class names based on DeepFashion2/FashionPedia categories
    // These should match the classes in the trained fashion YOLO model
    private static readonly Dictionary<int, string> FashionClassNames = new()
    {
        // Upper body clothing
        { 0, "short_sleeve_top" },
        { 1, "long_sleeve_top" },
        { 2, "short_sleeve_outwear" },
        { 3, "long_sleeve_outwear" },
        { 4, "vest" },
        { 5, "sling" },
        // Lower body clothing
        { 6, "shorts" },
        { 7, "trousers" },
        { 8, "skirt" },
        // Full body
        { 9, "short_sleeve_dress" },
        { 10, "long_sleeve_dress" },
        { 11, "vest_dress" },
        { 12, "sling_dress" }
    };

    // Map fashion model classes to common names for matching
    private static readonly Dictionary<string, List<string>> ClassNameMapping = new()
    {
        { "short_sleeve_top", new() { "shirt", "t-shirt", "tshirt", "top", "blouse" } },
        { "long_sleeve_top", new() { "shirt", "top", "blouse", "sweater" } },
        { "short_sleeve_outwear", new() { "jacket", "coat", "outwear", "blazer", "vest" } },
        { "long_sleeve_outwear", new() { "jacket", "coat", "outwear", "blazer", "cardigan", "hoodie", "sweater" } },
        { "vest", new() { "vest", "gilet" } },
        { "sling", new() { "tank top", "camisole", "top" } },
        { "shorts", new() { "shorts" } },
        { "trousers", new() { "pants", "trousers", "jeans", "slacks", "leggings" } },
        { "skirt", new() { "skirt" } },
        { "short_sleeve_dress", new() { "dress" } },
        { "long_sleeve_dress", new() { "dress" } },
        { "vest_dress", new() { "dress" } },
        { "sling_dress", new() { "dress" } }
    };

    public ClothingDetectionService(
        IConfiguration configuration,
        ILogger<ClothingDetectionService> logger)
    {
        _configuration = configuration;
        _logger = logger;
        
        // Read configuration
        _enabled = configuration.GetValue("ClothingDetection:Enabled", false);
        _modelPath = configuration["ClothingDetection:ModelPath"];
        _confidenceThreshold = configuration.GetValue("ClothingDetection:ConfidenceThreshold", 0.5f);

        // Only try to load model if enabled
        if (!_enabled)
        {
            _logger.LogInformation("Clothing detection is disabled in configuration");
            return;
        }

        // Try to load ONNX model if configured and exists
        if (!string.IsNullOrEmpty(_modelPath) && File.Exists(_modelPath))
        {
            try
            {
                _session = new InferenceSession(_modelPath);
                _logger.LogInformation("Clothing detection model loaded from {ModelPath}", _modelPath);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load clothing detection model from {ModelPath}. Clothing detection will be disabled.", _modelPath);
                _enabled = false;
            }
        }
        else
        {
            if (_enabled && string.IsNullOrEmpty(_modelPath))
            {
                _logger.LogWarning("Clothing detection is enabled but no model path is configured. Clothing detection will be disabled.");
            }
            else if (_enabled)
            {
                _logger.LogWarning("Clothing detection model not found at {ModelPath}. Clothing detection will be disabled.", _modelPath);
            }
            _enabled = false;
        }
    }

    public async Task<List<DetectedItem>> DetectClothingAsync(byte[] imageBytes, float? confidenceThreshold = null)
    {
        if (!_enabled || _session == null)
        {
            // Return empty list if service is not enabled or model not loaded
            return new List<DetectedItem>();
        }

        var threshold = confidenceThreshold ?? _confidenceThreshold;

        try
        {
            using var image = Image.Load<Rgb24>(imageBytes);
            var originalWidth = image.Width;
            var originalHeight = image.Height;

            // Resize image to model input size
            image.Mutate(x => x.Resize(InputWidth, InputHeight));

            // Prepare input tensor in NCHW format (batch, channels, height, width)
            var input = new DenseTensor<float>(new[] { 1, 3, InputHeight, InputWidth });
            for (int y = 0; y < InputHeight; y++)
            {
                for (int x = 0; x < InputWidth; x++)
                {
                    var pixel = image[x, y];
                    input[0, 0, y, x] = pixel.R / 255f;
                    input[0, 1, y, x] = pixel.G / 255f;
                    input[0, 2, y, x] = pixel.B / 255f;
                }
            }

            // Run inference
            var inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor("images", input)
            };

            using var results = _session.Run(inputs);
            var output = results.First().AsTensor<float>();

            // Parse YOLO output and extract clothing items
            var detections = ParseYoloOutput(output, threshold);
            
            _logger.LogInformation("Detected {ClothingCount} clothing items", detections.Count);
            return detections;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error detecting clothing items");
            return new List<DetectedItem>();
        }
    }

    private List<DetectedItem> ParseYoloOutput(Tensor<float> output, float confidenceThreshold)
    {
        var detections = new List<DetectedItem>();
        var dims = output.Dimensions.ToArray();
        
        // YOLO output format: [batch, classes + 4, num_detections]
        // where the first 4 values are bbox coordinates (cx, cy, w, h)
        // and the remaining values are class probabilities
        
        int numClasses = FashionClassNames.Count;
        int numDetections = dims.Length > 2 ? dims[2] : 8400;
        
        for (int i = 0; i < numDetections; i++)
        {
            // Get class scores (skip first 4 bbox coordinates)
            float maxScore = 0;
            int maxClass = 0;
            
            for (int c = 0; c < numClasses; c++)
            {
                var score = output[0, 4 + c, i];
                if (score > maxScore)
                {
                    maxScore = score;
                    maxClass = c;
                }
            }

            // Check if detection meets confidence threshold
            if (maxScore >= confidenceThreshold && FashionClassNames.TryGetValue(maxClass, out var className))
            {
                // Convert fashion class to common names for matching
                var commonNames = ClassNameMapping.TryGetValue(className, out var names) ? names : new List<string> { className };
                
                // Use the first common name as the primary label
                var label = commonNames.FirstOrDefault() ?? className;
                
                detections.Add(new DetectedItem(label, maxScore));
                
                _logger.LogDebug("Detected {ClassName} ({Label}) with confidence {Confidence:F3}", 
                    className, label, maxScore);
            }
        }

        // Apply Non-Maximum Suppression to remove duplicate detections
        return ApplyNMS(detections);
    }

    private List<DetectedItem> ApplyNMS(List<DetectedItem> items)
    {
        // Group similar items and keep the one with highest confidence
        var result = new List<DetectedItem>();
        var grouped = items.GroupBy(i => i.Label.ToLowerInvariant());
        
        foreach (var group in grouped)
        {
            // Keep the detection with highest confidence for each label
            var best = group.OrderByDescending(i => i.Confidence).First();
            result.Add(best);
        }
        
        return result;
    }

    public void Dispose()
    {
        _session?.Dispose();
        GC.SuppressFinalize(this);
    }
}
