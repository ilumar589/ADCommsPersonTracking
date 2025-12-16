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
    private bool _enabled;  // Not readonly because it can be disabled if model loading fails
    private readonly float _confidenceThreshold;
    private const int InputWidth = 640;
    private const int InputHeight = 640;
    private const float IouThreshold = 0.5f;
    
    // Fashionpedia class names from keremberke/yolov8m-fashion-detection model
    // This model is trained on Fashionpedia dataset with 27 clothing categories
    private static readonly Dictionary<int, string> FashionClassNames = new()
    {
        { 0, "shirt" },
        { 1, "top" },
        { 2, "sweater" },
        { 3, "cardigan" },
        { 4, "jacket" },
        { 5, "vest" },
        { 6, "pants" },
        { 7, "shorts" },
        { 8, "skirt" },
        { 9, "coat" },
        { 10, "dress" },
        { 11, "jumpsuit" },
        { 12, "cape" },
        { 13, "glasses" },
        { 14, "hat" },
        { 15, "headband" },
        { 16, "tie" },
        { 17, "glove" },
        { 18, "watch" },
        { 19, "belt" },
        { 20, "leg warmer" },
        { 21, "tights" },
        { 22, "sock" },
        { 23, "shoe" },
        { 24, "bag" },
        { 25, "scarf" },
        { 26, "umbrella" }
    };

    // Map fashion model classes to common names for matching
    private static readonly Dictionary<string, List<string>> ClassNameMapping = new()
    {
        { "shirt", new() { "shirt", "t-shirt", "tshirt", "blouse" } },
        { "top", new() { "top", "blouse", "tank top" } },
        { "sweater", new() { "sweater", "pullover", "jumper" } },
        { "cardigan", new() { "cardigan", "sweater" } },
        { "jacket", new() { "jacket", "blazer", "coat" } },
        { "vest", new() { "vest", "gilet", "waistcoat" } },
        { "pants", new() { "pants", "trousers", "jeans", "slacks" } },
        { "shorts", new() { "shorts" } },
        { "skirt", new() { "skirt" } },
        { "coat", new() { "coat", "jacket", "overcoat" } },
        { "dress", new() { "dress", "gown" } },
        { "jumpsuit", new() { "jumpsuit", "overalls", "romper" } },
        { "cape", new() { "cape", "cloak", "poncho" } },
        { "glasses", new() { "glasses", "sunglasses", "eyeglasses" } },
        { "hat", new() { "hat", "cap", "beanie" } },
        { "headband", new() { "headband", "hairband" } },
        { "tie", new() { "tie", "necktie", "bowtie" } },
        { "glove", new() { "glove", "gloves" } },
        { "watch", new() { "watch", "wristwatch" } },
        { "belt", new() { "belt" } },
        { "leg warmer", new() { "leg warmer", "legwarmer" } },
        { "tights", new() { "tights", "leggings", "pantyhose" } },
        { "sock", new() { "sock", "socks" } },
        { "shoe", new() { "shoe", "shoes", "sneaker", "boot" } },
        { "bag", new() { "bag", "purse", "handbag" } },
        { "scarf", new() { "scarf" } },
        { "umbrella", new() { "umbrella" } }
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

    public async Task<List<DetectedClothingItem>> DetectClothingAsync(byte[] imageBytes, float? confidenceThreshold = null)
    {
        if (!_enabled || _session == null)
        {
            // Return empty list if service is not enabled or model not loaded
            return new List<DetectedClothingItem>();
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

            // Parse YOLO output and extract clothing items with bounding boxes
            var detections = ParseYoloOutput(output, threshold, originalWidth, originalHeight);
            
            _logger.LogInformation("Detected {ClothingCount} clothing items", detections.Count);
            return detections;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error detecting clothing items");
            return new List<DetectedClothingItem>();
        }
    }

    private List<DetectedClothingItem> ParseYoloOutput(Tensor<float> output, float confidenceThreshold, int originalWidth, int originalHeight)
    {
        var detections = new List<DetectedClothingItem>();
        var dims = output.Dimensions.ToArray();
        
        _logger.LogDebug("YOLO output dimensions: [{Dims}]", string.Join(", ", dims));
        
        // Detect model type based on output dimensions
        // YOLOv8/YOLOv11 format: [batch, classes + 4, num_detections]
        // where the first 4 values are bbox coordinates (cx, cy, w, h)
        // and the remaining values are class probabilities
        
        int numClasses = FashionClassNames.Count;
        int numDetections = dims.Length > 2 ? dims[2] : 8400;
        int outputChannels = dims.Length > 1 ? dims[1] : (4 + numClasses);
        
        _logger.LogDebug("Parsing fashion model output: {NumDetections} detections, {NumClasses} classes, {OutputChannels} output channels", 
            numDetections, numClasses, outputChannels);
        
        // Calculate scale factors for bounding box conversion
        float scaleX = (float)originalWidth / InputWidth;
        float scaleY = (float)originalHeight / InputHeight;
        
        for (int i = 0; i < numDetections; i++)
        {
            // Get bounding box coordinates (cx, cy, w, h)
            float cx = output[0, 0, i];
            float cy = output[0, 1, i];
            float w = output[0, 2, i];
            float h = output[0, 3, i];
            
            // Get class scores (skip first 4 bbox coordinates)
            float maxScore = 0;
            int maxClass = 0;
            
            for (int c = 0; c < numClasses && (4 + c) < outputChannels; c++)
            {
                var score = output[0, 4 + c, i];
                if (score > maxScore)
                {
                    maxScore = score;
                    maxClass = c;
                }
            }
            
            // Log raw scores before threshold filtering for diagnostics
            if (maxScore > 0.01f) // Only log scores above 1% to reduce noise
            {
                var className = FashionClassNames.TryGetValue(maxClass, out var cn) ? cn : $"class_{maxClass}";
                _logger.LogDebug("Detection {Index}: class={ClassName}, score={Score:F3}, bbox=[{Cx:F1}, {Cy:F1}, {W:F1}, {H:F1}]",
                    i, className, maxScore, cx, cy, w, h);
            }

            // Check if detection meets confidence threshold
            if (maxScore >= confidenceThreshold && FashionClassNames.TryGetValue(maxClass, out var detectedClassName))
            {
                // Convert fashion class to common names for matching
                var commonNames = ClassNameMapping.TryGetValue(detectedClassName, out var names) ? names : new List<string> { detectedClassName };
                
                // Use the first common name as the primary label
                var label = commonNames.FirstOrDefault() ?? detectedClassName;
                
                // Convert center coordinates to top-left coordinates and scale to original image size
                float x = (cx - w / 2) * scaleX;
                float y = (cy - h / 2) * scaleY;
                float width = w * scaleX;
                float height = h * scaleY;
                
                // Ensure bounding box is within image bounds
                x = Math.Max(0, x);
                y = Math.Max(0, y);
                width = Math.Min(width, originalWidth - x);
                height = Math.Min(height, originalHeight - y);
                
                var boundingBox = new BoundingBox
                {
                    X = x,
                    Y = y,
                    Width = width,
                    Height = height,
                    Confidence = maxScore,
                    Label = label
                };
                
                detections.Add(new DetectedClothingItem(label, maxScore, boundingBox));
                
                _logger.LogDebug("Detected {ClassName} ({Label}) with confidence {Confidence:F3} at [{X:F1}, {Y:F1}, {W:F1}, {H:F1}]", 
                    detectedClassName, label, maxScore, x, y, width, height);
            }
        }

        _logger.LogInformation("Fashion model found {Count} detections above threshold {Threshold:F2}", 
            detections.Count, confidenceThreshold);

        // Apply Non-Maximum Suppression to remove duplicate detections
        return ApplyNMS(detections);
    }

    private List<DetectedClothingItem> ApplyNMS(List<DetectedClothingItem> items)
    {
        if (items.Count == 0)
            return items;

        var result = new List<DetectedClothingItem>();
        var sorted = items.OrderByDescending(i => i.Confidence).ToList();

        foreach (var item in sorted)
        {
            bool shouldAdd = true;

            // Check if this item overlaps significantly with any already selected item
            foreach (var selected in result)
            {
                // Calculate IoU (Intersection over Union)
                float iou = CalculateIoU(item.BoundingBox, selected.BoundingBox);

                // If IoU is high and it's the same type of clothing, skip this detection
                if (iou > IouThreshold && item.Label.Equals(selected.Label, StringComparison.OrdinalIgnoreCase))
                {
                    shouldAdd = false;
                    break;
                }
            }

            if (shouldAdd)
            {
                result.Add(item);
            }
        }

        return result;
    }

    private float CalculateIoU(BoundingBox box1, BoundingBox box2)
    {
        // Calculate intersection
        float x1 = Math.Max(box1.X, box2.X);
        float y1 = Math.Max(box1.Y, box2.Y);
        float x2 = Math.Min(box1.X + box1.Width, box2.X + box2.Width);
        float y2 = Math.Min(box1.Y + box1.Height, box2.Y + box2.Height);

        float intersectionWidth = Math.Max(0, x2 - x1);
        float intersectionHeight = Math.Max(0, y2 - y1);
        float intersectionArea = intersectionWidth * intersectionHeight;

        // Calculate union
        float box1Area = box1.Width * box1.Height;
        float box2Area = box2.Width * box2.Height;
        float unionArea = box1Area + box2Area - intersectionArea;

        // Calculate IoU
        return unionArea > 0 ? intersectionArea / unionArea : 0;
    }

    public void Dispose()
    {
        _session?.Dispose();
        GC.SuppressFinalize(this);
    }
}
