using ADCommsPersonTracking.Api.Logging;
using ADCommsPersonTracking.Api.Models;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace ADCommsPersonTracking.Api.Services;

public class ObjectDetectionService : IObjectDetectionService, IDisposable
{
    private readonly ILogger<ObjectDetectionService> _logger;
    private InferenceSession? _session;
    private readonly string _modelPath;
    private const int InputWidth = 640;
    private const int InputHeight = 640;
    private const float ConfidenceThreshold = 0.45f;
    private readonly float _accessoryConfidenceThreshold;
    private const float IouThreshold = 0.5f;
    private const int MaxDetections = 8400; // YOLO11 output detections
    private const int NumCocoClasses = 80; // Number of classes in COCO dataset
    
    // COCO class IDs for accessories
    private static readonly HashSet<int> AccessoryClassIds = new() { 24, 26, 27, 28 }; // backpack, handbag, tie, suitcase
    private static readonly Dictionary<int, string> CocoClassNames = new()
    {
        { 0, "person" },
        { 24, "backpack" },
        { 26, "handbag" },
        { 27, "tie" },
        { 28, "suitcase" }
    };

    public ObjectDetectionService(IConfiguration configuration, ILogger<ObjectDetectionService> logger)
    {
        _logger = logger;
        _modelPath = configuration["ObjectDetection:ModelPath"] ?? "models/yolo11x.onnx";
        
        // Read accessory confidence threshold from configuration, default to 0.25
        _accessoryConfidenceThreshold = configuration.GetValue<float>("ObjectDetection:AccessoryConfidenceThreshold", 0.25f);
        
        // Initialize model if it exists
        if (File.Exists(_modelPath))
        {
            try
            {
                _session = new InferenceSession(_modelPath);
                _logger.LogModelLoaded(_modelPath);
            }
            catch (Exception ex)
            {
                _logger.LogModelLoadWarning(_modelPath, ex);
            }
        }
        else
        {
            _logger.LogModelNotFound(_modelPath);
        }
    }

    public async Task<List<BoundingBox>> DetectPersonsAsync(byte[] imageBytes)
    {
        if (_session == null)
        {
            throw new InvalidOperationException(
                $"Inference session is not initialized. The ONNX model was not found at '{_modelPath}'. " +
                "When running from Aspire, ensure the yolo-model-export container has completed and the model exists in the shared volume.");
        }

        try
        {

            using var image = Image.Load<Rgb24>(imageBytes);
            var originalWidth = image.Width;
            var originalHeight = image.Height;

            // Resize image to model input size
            image.Mutate(x => x.Resize(InputWidth, InputHeight));

            // Prepare input tensor
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

            // Parse YOLO output and filter for persons (class 0 in COCO dataset)
            var detections = ParseYoloOutput(output, originalWidth, originalHeight);
            
            _logger.LogDetectedPersons(detections.Count);
            return detections;
        }
        catch (Exception ex)
        {
            _logger.LogObjectDetectionError(ex);
            throw; // Re-throw instead of returning empty list
        }
    }

    public async Task<List<DetectedObject>> DetectObjectsAsync(byte[] imageBytes)
    {
        if (_session == null)
        {
            throw new InvalidOperationException(
                $"Inference session is not initialized. The ONNX model was not found at '{_modelPath}'. " +
                "When running from Aspire, ensure the yolo-model-export container has completed and the model exists in the shared volume.");
        }

        try
        {
            using var image = Image.Load<Rgb24>(imageBytes);
            var originalWidth = image.Width;
            var originalHeight = image.Height;

            // Resize image to model input size
            image.Mutate(x => x.Resize(InputWidth, InputHeight));

            // Prepare input tensor
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

            // Parse YOLO output for persons and accessories
            var detections = ParseYoloOutputForObjects(output, originalWidth, originalHeight);
            
            _logger.LogInformation("Detected {PersonCount} persons and {AccessoryCount} accessories", 
                detections.Count(d => d.ClassId == 0), 
                detections.Count(d => d.ClassId != 0));
            
            return detections;
        }
        catch (Exception ex)
        {
            _logger.LogObjectDetectionError(ex);
            throw;
        }
    }

    private List<BoundingBox> ParseYoloOutput(Tensor<float> output, int originalWidth, int originalHeight)
    {
        var detections = new List<BoundingBox>();
        var dims = output.Dimensions.ToArray();
        
        // YOLO11 output format: [batch, 84, 8400] where 84 = 4 (bbox) + 80 (classes)
        // YOLO11 uses the same output format as YOLOv8
        int numDetections = dims.Length > 2 ? dims[2] : MaxDetections;
        
        for (int i = 0; i < numDetections; i++)
        {
            // Get class scores (skip first 4 bbox coordinates)
            float maxScore = 0;
            int maxClass = 0;
            
            for (int c = 0; c < NumCocoClasses; c++)
            {
                var score = output[0, 4 + c, i];
                if (score > maxScore)
                {
                    maxScore = score;
                    maxClass = c;
                }
            }

            // Check if it's a person (class 0) and meets confidence threshold
            if (maxClass == 0 && maxScore >= ConfidenceThreshold)
            {
                var cx = output[0, 0, i];
                var cy = output[0, 1, i];
                var w = output[0, 2, i];
                var h = output[0, 3, i];

                // Convert from model coordinates to original image coordinates
                var x = (cx - w / 2) * originalWidth / InputWidth;
                var y = (cy - h / 2) * originalHeight / InputHeight;
                var width = w * originalWidth / InputWidth;
                var height = h * originalHeight / InputHeight;

                detections.Add(new BoundingBox
                {
                    X = x,
                    Y = y,
                    Width = width,
                    Height = height,
                    Confidence = maxScore,
                    Label = "person"
                });
            }
        }

        // Apply Non-Maximum Suppression
        return ApplyNMS(detections);
    }

    private List<DetectedObject> ParseYoloOutputForObjects(Tensor<float> output, int originalWidth, int originalHeight)
    {
        var detections = new List<DetectedObject>();
        var dims = output.Dimensions.ToArray();
        
        // YOLO11 output format: [batch, 84, 8400] where 84 = 4 (bbox) + 80 (classes)
        int numDetections = dims.Length > 2 ? dims[2] : MaxDetections;
        
        int personCount = 0;
        int accessoryCount = 0;
        int backpacksFiltered = 0;
        
        for (int i = 0; i < numDetections; i++)
        {
            // Get class scores (skip first 4 bbox coordinates)
            float maxScore = 0;
            int maxClass = 0;
            
            for (int c = 0; c < NumCocoClasses; c++)
            {
                var score = output[0, 4 + c, i];
                if (score > maxScore)
                {
                    maxScore = score;
                    maxClass = c;
                }
            }

            // Log potential detections for persons and accessories (even if filtered)
            if (maxClass == 0 || AccessoryClassIds.Contains(maxClass))
            {
                var threshold = maxClass == 0 ? ConfidenceThreshold : _accessoryConfidenceThreshold;
                var passes = maxScore >= threshold;
                _logger.LogRawYoloDetection(maxClass, maxScore, threshold, passes);
                
                // Specifically track backpack filtering
                if (maxClass == 24 && !passes)
                {
                    _logger.LogBackpackFilteredByConfidence(maxScore, threshold);
                    backpacksFiltered++;
                }
            }

            // Check if it's a person (class 0) or an accessory class and meets confidence threshold
            var detectionThreshold = maxClass == 0 ? ConfidenceThreshold : _accessoryConfidenceThreshold;
            if ((maxClass == 0 || AccessoryClassIds.Contains(maxClass)) && maxScore >= detectionThreshold)
            {
                var cx = output[0, 0, i];
                var cy = output[0, 1, i];
                var w = output[0, 2, i];
                var h = output[0, 3, i];

                // Convert from model coordinates to original image coordinates
                var x = (cx - w / 2) * originalWidth / InputWidth;
                var y = (cy - h / 2) * originalHeight / InputHeight;
                var width = w * originalWidth / InputWidth;
                var height = h * originalHeight / InputHeight;

                var label = CocoClassNames.TryGetValue(maxClass, out var className) ? className : $"class_{maxClass}";

                detections.Add(new DetectedObject
                {
                    ClassId = maxClass,
                    ObjectType = label,
                    BoundingBox = new BoundingBox
                    {
                        X = x,
                        Y = y,
                        Width = width,
                        Height = height,
                        Confidence = maxScore,
                        Label = label
                    }
                });
                
                if (maxClass == 0) personCount++;
                else if (AccessoryClassIds.Contains(maxClass)) accessoryCount++;
            }
        }

        _logger.LogYoloParsingComplete(numDetections, personCount, accessoryCount);
        
        if (backpacksFiltered > 0)
        {
            _logger.LogInformation("Filtered out {BackpackCount} backpack detections due to low confidence", backpacksFiltered);
        }

        // Apply Non-Maximum Suppression per class
        return ApplyNMSPerClass(detections);
    }

    private List<DetectedObject> ApplyNMSPerClass(List<DetectedObject> objects)
    {
        var result = new List<DetectedObject>();
        
        // Group by class and apply NMS separately for each class
        var groupedByClass = objects.GroupBy(o => o.ClassId);
        
        foreach (var classGroup in groupedByClass)
        {
            var sortedObjects = classGroup.OrderByDescending(o => o.BoundingBox.Confidence).ToList();
            
            while (sortedObjects.Any())
            {
                var current = sortedObjects.First();
                result.Add(current);
                sortedObjects.RemoveAt(0);
                
                sortedObjects = sortedObjects.Where(obj => 
                    CalculateIoU(current.BoundingBox, obj.BoundingBox) < IouThreshold).ToList();
            }
        }
        
        return result;
    }

    private List<BoundingBox> ApplyNMS(List<BoundingBox> boxes)
    {
        var result = new List<BoundingBox>();
        var sortedBoxes = boxes.OrderByDescending(b => b.Confidence).ToList();

        while (sortedBoxes.Any())
        {
            var current = sortedBoxes.First();
            result.Add(current);
            sortedBoxes.RemoveAt(0);

            sortedBoxes = sortedBoxes.Where(box => 
                CalculateIoU(current, box) < IouThreshold).ToList();
        }

        return result;
    }

    private float CalculateIoU(BoundingBox box1, BoundingBox box2)
    {
        var x1 = Math.Max(box1.X, box2.X);
        var y1 = Math.Max(box1.Y, box2.Y);
        var x2 = Math.Min(box1.X + box1.Width, box2.X + box2.Width);
        var y2 = Math.Min(box1.Y + box1.Height, box2.Y + box2.Height);

        var intersectionArea = Math.Max(0, x2 - x1) * Math.Max(0, y2 - y1);
        var box1Area = box1.Width * box1.Height;
        var box2Area = box2.Width * box2.Height;
        var unionArea = box1Area + box2Area - intersectionArea;

        return unionArea > 0 ? intersectionArea / unionArea : 0;
    }

    public void Dispose()
    {
        _session?.Dispose();
        GC.SuppressFinalize(this);
    }
}
