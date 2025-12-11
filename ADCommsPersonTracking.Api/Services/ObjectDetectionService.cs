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
    private const float IouThreshold = 0.5f;

    public ObjectDetectionService(IConfiguration configuration, ILogger<ObjectDetectionService> logger)
    {
        _logger = logger;
        _modelPath = configuration["ObjectDetection:ModelPath"] ?? "models/yolo11n.onnx";
        
        // Initialize model if it exists
        if (File.Exists(_modelPath))
        {
            try
            {
                _session = new InferenceSession(_modelPath);
                _logger.LogInformation("YOLO11 ONNX model loaded successfully from {ModelPath}", _modelPath);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not load YOLO11 ONNX model from {ModelPath}. Detection will use mock data.", _modelPath);
            }
        }
        else
        {
            _logger.LogWarning("YOLO11 ONNX model not found at {ModelPath}. Detection will use mock data.", _modelPath);
        }
    }

    public async Task<List<BoundingBox>> DetectPersonsAsync(byte[] imageBytes)
    {
        try
        {
            // If no model is loaded, return mock detections for demonstration
            if (_session == null)
            {
                _logger.LogInformation("Using mock detection data");
                return await Task.FromResult(GenerateMockDetections());
            }

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
            
            _logger.LogInformation("Detected {Count} persons in frame", detections.Count);
            return detections;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during object detection");
            return new List<BoundingBox>();
        }
    }

    private List<BoundingBox> ParseYoloOutput(Tensor<float> output, int originalWidth, int originalHeight)
    {
        var detections = new List<BoundingBox>();
        var dims = output.Dimensions.ToArray();
        
        // YOLO11 output format: [batch, 84, 8400] where 84 = 4 (bbox) + 80 (classes)
        // YOLO11 uses the same output format as YOLOv8
        int numDetections = dims.Length > 2 ? dims[2] : 8400;
        
        for (int i = 0; i < numDetections; i++)
        {
            // Get class scores (skip first 4 bbox coordinates)
            float maxScore = 0;
            int maxClass = 0;
            
            for (int c = 0; c < 80; c++)
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

    private List<BoundingBox> GenerateMockDetections()
    {
        // Return sample detections for demonstration when no model is available
        return new List<BoundingBox>
        {
            new BoundingBox
            {
                X = 100,
                Y = 150,
                Width = 120,
                Height = 280,
                Confidence = 0.85f,
                Label = "person"
            }
        };
    }

    public void Dispose()
    {
        _session?.Dispose();
    }
}
