using ADCommsPersonTracking.Api.Logging;
using ADCommsPersonTracking.Api.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace ADCommsPersonTracking.Api.Services;

/// <summary>
/// Heuristics-based physical attribute estimator.
/// Uses bounding box dimensions and image analysis to estimate physical characteristics.
/// </summary>
public class PhysicalAttributeEstimator : IPhysicalAttributeEstimator
{
    private readonly ILogger<PhysicalAttributeEstimator> _logger;

    // Average human height for reference (in meters)
    private const float AverageHumanHeight = 1.7f;

    // Thresholds for height categorization (relative to frame height)
    private const float TallThreshold = 0.65f;
    private const float ShortThreshold = 0.35f;

    // Thresholds for build categorization (aspect ratio)
    private const float SlimAspectRatio = 0.35f;
    private const float HeavyAspectRatio = 0.50f;

    // Threshold for hair length detection (dark pixels in upper portion)
    private const float LongHairThreshold = 0.15f;
    private const float ShortHairThreshold = 0.08f;
    
    // Brightness threshold for detecting dark pixels (potential hair)
    private const int DarkPixelBrightnessThreshold = 80;

    public PhysicalAttributeEstimator(ILogger<PhysicalAttributeEstimator> logger)
    {
        _logger = logger;
    }

    public async Task<PhysicalAttributes> EstimateAttributesAsync(byte[] imageBytes, BoundingBox personBox, int imageHeight, int imageWidth)
    {
        try
        {
            var attributes = new PhysicalAttributes();

            // Estimate relative height based on bounding box height relative to frame
            var relativeHeight = personBox.Height / imageHeight;
            attributes.HeightCategory = EstimateHeightCategory(relativeHeight);
            attributes.EstimatedHeightMeters = EstimateHeightInMeters(relativeHeight);

            // Estimate build based on aspect ratio
            attributes.AspectRatio = personBox.Width / personBox.Height;
            attributes.BuildCategory = EstimateBuildCategory(attributes.AspectRatio);

            // Estimate hair length from upper portion of person crop
            attributes.HairLength = await EstimateHairLengthAsync(imageBytes, personBox);

            // Populate all attributes list
            attributes.AllAttributes = new List<string>
            {
                attributes.HeightCategory,
                attributes.BuildCategory,
                $"{attributes.HairLength} hair"
            };

            _logger.LogDebug("Estimated attributes: Height={Height}, Build={Build}, Hair={Hair}", 
                attributes.HeightCategory, attributes.BuildCategory, attributes.HairLength);

            return attributes;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error estimating physical attributes");
            return new PhysicalAttributes();
        }
    }

    public bool MatchesCriteria(PhysicalAttributes attributes, List<string> searchAttributes, HeightInfo? searchHeight)
    {
        if (searchAttributes.Count == 0 && searchHeight == null)
        {
            return true; // No physical attribute criteria
        }

        // Check height if specified
        if (searchHeight != null)
        {
            // Allow 10cm tolerance for height matching
            var heightDiff = Math.Abs(attributes.EstimatedHeightMeters - searchHeight.Value.Meters);
            if (heightDiff > 0.10f)
            {
                return false;
            }
        }

        // Check if any search attribute matches
        if (searchAttributes.Count > 0)
        {
            var lowerSearchAttrs = searchAttributes.Select(a => a.ToLowerInvariant()).ToList();
            var lowerAttrs = attributes.AllAttributes.Select(a => a.ToLowerInvariant()).ToList();

            foreach (var searchAttr in lowerSearchAttrs)
            {
                // Check for direct match
                if (lowerAttrs.Any(a => a.Contains(searchAttr)))
                {
                    return true;
                }

                // Check for synonym matches
                if (IsAttributeMatch(searchAttr, attributes))
                {
                    return true;
                }
            }

            return false;
        }

        return true;
    }

    private string EstimateHeightCategory(float relativeHeight)
    {
        if (relativeHeight >= TallThreshold)
            return "tall";
        else if (relativeHeight <= ShortThreshold)
            return "short";
        else
            return "medium height";
    }

    private float EstimateHeightInMeters(float relativeHeight)
    {
        // Rough estimation: assume camera is at eye level (1.6m) and person fills certain portion of frame
        // This is a very rough heuristic and would need calibration for real-world use
        var estimatedHeight = AverageHumanHeight * (relativeHeight / 0.5f);
        return Math.Clamp(estimatedHeight, 1.4f, 2.1f); // Clamp to realistic human heights
    }

    private string EstimateBuildCategory(float aspectRatio)
    {
        if (aspectRatio <= SlimAspectRatio)
            return "slim";
        else if (aspectRatio >= HeavyAspectRatio)
            return "heavy";
        else
            return "medium build";
    }

    private async Task<string> EstimateHairLengthAsync(byte[] imageBytes, BoundingBox personBox)
    {
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
                return "unknown";
            }

            // Analyze the top 20% of the person crop (head area)
            var headHeight = (int)(height * 0.20);
            var headY = y;

            // Count dark pixels that might indicate hair
            int darkPixelCount = 0;
            int totalPixels = 0;
            var step = 3; // Sample every 3rd pixel for efficiency

            for (int py = headY; py < headY + headHeight && py < image.Height; py += step)
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

            if (totalPixels == 0)
            {
                return "unknown";
            }

            var darkPixelRatio = (float)darkPixelCount / totalPixels;

            if (darkPixelRatio >= LongHairThreshold)
                return "long";
            else if (darkPixelRatio >= ShortHairThreshold)
                return "medium";
            else
                return "short";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error estimating hair length");
            return "unknown";
        }
    }

    private bool IsAttributeMatch(string searchAttr, PhysicalAttributes attributes)
    {
        var lower = searchAttr.ToLowerInvariant();

        // Height synonyms
        if (lower.Contains("tall") || lower.Contains("very tall"))
            return attributes.HeightCategory == "tall";
        if (lower.Contains("short") || lower.Contains("very short"))
            return attributes.HeightCategory == "short";
        
        // Build synonyms
        if (lower.Contains("thin") || lower.Contains("slender") || lower.Contains("lean") || lower.Contains("skinny"))
            return attributes.BuildCategory == "slim";
        if (lower.Contains("large") || lower.Contains("heavy") || lower.Contains("overweight") || lower.Contains("bulky"))
            return attributes.BuildCategory == "heavy";
        if (lower.Contains("athletic") || lower.Contains("fit") || lower.Contains("muscular"))
            return attributes.BuildCategory == "medium build";

        // Hair synonyms
        if (lower.Contains("long hair") || lower.Contains("long-haired"))
            return attributes.HairLength == "long";
        if (lower.Contains("short hair") || lower.Contains("short-haired") || lower.Contains("bald"))
            return attributes.HairLength == "short";

        return false;
    }
}
