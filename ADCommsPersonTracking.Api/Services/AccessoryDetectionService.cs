using ADCommsPersonTracking.Api.Logging;
using ADCommsPersonTracking.Api.Models;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace ADCommsPersonTracking.Api.Services;

/// <summary>
/// Service for detecting accessories and clothing items using ONNX models.
/// Currently uses a heuristic-based approach with placeholders for ONNX integration.
/// </summary>
public class AccessoryDetectionService : IAccessoryDetectionService, IDisposable
{
    private readonly ILogger<AccessoryDetectionService> _logger;
    private readonly IConfiguration _configuration;
    private InferenceSession? _session;
    private readonly float _confidenceThreshold;
    
    // Thresholds for spatial association
    private const float MinIouThreshold = 0.01f; // Minimum IoU for association
    private const float ExtendedBoxLeftRightFactor = 0.2f; // Extend person box by 20% left/right for backpacks
    private const float ExtendedBoxTopFactor = 0.1f; // Extend person box by 10% at top
    private const float ExtendedBoxWidthMultiplier = 1.4f; // Total width = 140% of original
    private const float ExtendedBoxHeightMultiplier = 1.2f; // Total height = 120% of original
    
    // Accessory and clothing type classifications
    private static readonly HashSet<string> AccessoryTypes = new(StringComparer.OrdinalIgnoreCase) 
    { 
        "backpack", "handbag", "suitcase" 
    };
    private static readonly HashSet<string> ClothingTypes = new(StringComparer.OrdinalIgnoreCase) 
    { 
        "tie" 
    };

    // Known accessory and clothing keywords for basic detection
    // In a production system, this would be replaced by actual ONNX model inference
    private static readonly HashSet<string> AccessoryKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "backpack", "bag", "handbag", "purse", "hat", "cap", 
        "glasses", "sunglasses", "watch", "umbrella", "briefcase"
    };

    private static readonly HashSet<string> ClothingKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "jacket", "coat", "dress", "shorts", "skirt", "suit", 
        "pants", "jeans", "shirt", "hoodie"
    };

    // Thresholds for heuristic detection
    private const int DarkPixelBrightnessThreshold = 60;
    private const float DarkPixelRatioForHat = 0.30f;

    public AccessoryDetectionService(
        IConfiguration configuration,
        ILogger<AccessoryDetectionService> logger)
    {
        _configuration = configuration;
        _logger = logger;
        _confidenceThreshold = configuration.GetValue("AccessoryDetection:ConfidenceThreshold", 0.5f);

        // Try to load ONNX model if configured
        var modelPath = configuration["AccessoryDetection:ModelPath"];
        if (!string.IsNullOrEmpty(modelPath) && File.Exists(modelPath))
        {
            try
            {
                _session = new InferenceSession(modelPath);
                _logger.LogInformation("Accessory detection model loaded from {ModelPath}", modelPath);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load accessory detection model from {ModelPath}. Using heuristic-based detection.", modelPath);
            }
        }
        else
        {
            _logger.LogInformation("No accessory detection model configured. Using heuristic-based detection.");
        }
    }

    public async Task<AccessoryDetectionResult> DetectAccessoriesAsync(byte[] imageBytes, BoundingBox personBox)
    {
        try
        {
            // If we have an ONNX model, use it
            if (_session != null)
            {
                return await DetectWithModelAsync(imageBytes, personBox);
            }

            // Otherwise, use heuristic-based detection
            return await DetectWithHeuristicsAsync(imageBytes, personBox);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error detecting accessories");
            return new AccessoryDetectionResult();
        }
    }

    public AccessoryDetectionResult DetectAccessoriesFromYolo(BoundingBox personBox, List<DetectedObject> allAccessories)
    {
        var result = new AccessoryDetectionResult();

        // Find accessories that are spatially associated with this person
        // Using IoU and proximity-based association
        foreach (var accessory in allAccessories)
        {
            if (IsAccessoryAssociatedWithPerson(personBox, accessory.BoundingBox))
            {
                // Map YOLO class to our accessory/clothing categories
                var detectedItem = new DetectedItem(accessory.ObjectType, accessory.BoundingBox.Confidence);
                
                // Classify based on type
                if (AccessoryTypes.Contains(accessory.ObjectType))
                {
                    result.Accessories.Add(detectedItem);
                }
                else if (ClothingTypes.Contains(accessory.ObjectType))
                {
                    result.ClothingItems.Add(detectedItem);
                }
            }
        }

        _logger.LogDebug("Associated {AccessoryCount} accessories with person", result.Accessories.Count);
        return result;
    }

    private bool IsAccessoryAssociatedWithPerson(BoundingBox personBox, BoundingBox accessoryBox)
    {
        // Check if accessory overlaps with person or is very close to person
        // Method 1: Check IoU (Intersection over Union)
        var iou = CalculateIoU(personBox, accessoryBox);
        if (iou > MinIouThreshold) // Even small overlap means association
        {
            return true;
        }

        // Method 2: Check if accessory is within extended person boundary
        // Accessories like backpacks may extend slightly beyond person box
        var extendedPersonBox = new BoundingBox
        {
            X = personBox.X - personBox.Width * ExtendedBoxLeftRightFactor,
            Y = personBox.Y - personBox.Height * ExtendedBoxTopFactor,
            Width = personBox.Width * ExtendedBoxWidthMultiplier,
            Height = personBox.Height * ExtendedBoxHeightMultiplier
        };

        var accessoryCenterX = accessoryBox.X + accessoryBox.Width / 2;
        var accessoryCenterY = accessoryBox.Y + accessoryBox.Height / 2;

        return accessoryCenterX >= extendedPersonBox.X &&
               accessoryCenterX <= extendedPersonBox.X + extendedPersonBox.Width &&
               accessoryCenterY >= extendedPersonBox.Y &&
               accessoryCenterY <= extendedPersonBox.Y + extendedPersonBox.Height;
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

    public bool MatchesCriteria(AccessoryDetectionResult detectionResult, List<string> searchClothing, List<string> searchAccessories)
    {
        if (searchClothing.Count == 0 && searchAccessories.Count == 0)
        {
            return true; // No accessory/clothing criteria
        }

        var detectedClothingLabels = detectionResult.ClothingItems
            .Select(c => c.Label.ToLowerInvariant())
            .ToHashSet();

        var detectedAccessoryLabels = detectionResult.Accessories
            .Select(a => a.Label.ToLowerInvariant())
            .ToHashSet();

        // Check clothing items
        foreach (var searchItem in searchClothing)
        {
            var lower = searchItem.ToLowerInvariant();
            if (detectedClothingLabels.Contains(lower) || detectedClothingLabels.Any(d => d.Contains(lower) || lower.Contains(d)))
            {
                return true;
            }
        }

        // Check accessories
        foreach (var searchItem in searchAccessories)
        {
            var lower = searchItem.ToLowerInvariant();
            if (detectedAccessoryLabels.Contains(lower) || detectedAccessoryLabels.Any(d => d.Contains(lower) || lower.Contains(d)))
            {
                return true;
            }
        }

        return false;
    }

    private async Task<AccessoryDetectionResult> DetectWithModelAsync(byte[] imageBytes, BoundingBox personBox)
    {
        // This is a placeholder for actual ONNX model inference
        // In a production system, this would:
        // 1. Crop the person region from the image
        // 2. Preprocess the image for the model
        // 3. Run inference
        // 4. Parse the model output to extract detected items

        await Task.CompletedTask; // Placeholder for async operation

        var result = new AccessoryDetectionResult();
        
        // Placeholder: would process model output here
        _logger.LogDebug("Model-based detection not fully implemented");

        return result;
    }

    private async Task<AccessoryDetectionResult> DetectWithHeuristicsAsync(byte[] imageBytes, BoundingBox personBox)
    {
        // Heuristic-based detection using simple image analysis
        // This is a placeholder that demonstrates the structure
        // In reality, without a proper ML model, detection is very limited

        await Task.CompletedTask; // Keep async signature

        var result = new AccessoryDetectionResult();

        try
        {
            using var memoryStream = new MemoryStream(imageBytes);
            using var image = await Image.LoadAsync<Rgb24>(memoryStream);

            // Ensure bounding box is within image bounds
            var x = Math.Max(0, (int)personBox.X);
            var y = Math.Max(0, (int)personBox.Y);
            var width = Math.Min((int)personBox.Width, image.Width - x);
            var height = Math.Min((int)personBox.Height, image.Height - y);

            if (width <= 0 || height <= 0)
            {
                return result;
            }

            // Simple heuristic: detect dark region at top (possible hat)
            // and check for rectangular regions that might be bags
            // This is very basic and should be replaced with actual ML model

            // Check for hat (dark pixels in top 15% of person)
            var hasHat = await CheckForHatAsync(image, x, y, width, height);
            if (hasHat)
            {
                result.Accessories.Add(new DetectedItem("hat", 0.6f));
            }

            // Check for bag/backpack (check bottom-left or back regions)
            var hasBag = await CheckForBagAsync(image, x, y, width, height);
            if (hasBag)
            {
                result.Accessories.Add(new DetectedItem("bag", 0.55f));
            }

            _logger.LogDebug("Heuristic detection found {AccessoryCount} accessories", result.Accessories.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in heuristic accessory detection");
        }

        return result;
    }

    private async Task<bool> CheckForHatAsync(Image<Rgb24> image, int x, int y, int width, int height)
    {
        await Task.CompletedTask;

        // Check top 15% of person bounding box for dark rectangular region
        var topHeight = (int)(height * 0.15);
        var darkPixelCount = 0;
        var totalPixels = 0;
        var step = 4;

        for (int py = y; py < y + topHeight && py < image.Height; py += step)
        {
            for (int px = x; px < x + width && px < image.Width; px += step)
            {
                var pixel = image[px, py];
                var brightness = (pixel.R + pixel.G + pixel.B) / 3;

                if (brightness < DarkPixelBrightnessThreshold)
                {
                    darkPixelCount++;
                }
                totalPixels++;
            }
        }

        // If more than 30% of top region is dark, might be a hat
        return totalPixels > 0 && (float)darkPixelCount / totalPixels > DarkPixelRatioForHat;
    }

    private async Task<bool> CheckForBagAsync(Image<Rgb24> image, int x, int y, int width, int height)
    {
        await Task.CompletedTask;

        // Check for presence of bag-like structures
        // This is a placeholder - real detection would need proper ML model
        
        // For now, just return false as we can't reliably detect bags without ML
        return false;
    }

    public void Dispose()
    {
        _session?.Dispose();
    }
}
