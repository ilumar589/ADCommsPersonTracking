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
