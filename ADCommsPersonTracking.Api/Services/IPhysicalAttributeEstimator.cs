using ADCommsPersonTracking.Api.Models;

namespace ADCommsPersonTracking.Api.Services;

/// <summary>
/// Service for estimating physical attributes from person detections using heuristics.
/// </summary>
public interface IPhysicalAttributeEstimator
{
    /// <summary>
    /// Estimate physical attributes for a person based on their bounding box and image crop.
    /// </summary>
    /// <param name="imageBytes">The full image bytes</param>
    /// <param name="personBox">The bounding box of the person</param>
    /// <param name="imageHeight">The height of the full image</param>
    /// <param name="imageWidth">The width of the full image</param>
    /// <returns>A list of estimated physical attributes</returns>
    Task<PhysicalAttributes> EstimateAttributesAsync(byte[] imageBytes, BoundingBox personBox, int imageHeight, int imageWidth);

    /// <summary>
    /// Check if the estimated attributes match the search criteria.
    /// </summary>
    /// <param name="attributes">The estimated physical attributes</param>
    /// <param name="criteria">The search criteria from the prompt</param>
    /// <returns>True if attributes match, false otherwise</returns>
    bool MatchesCriteria(PhysicalAttributes attributes, List<string> searchAttributes, HeightInfo? searchHeight);
}

/// <summary>
/// Physical attributes estimated for a person.
/// </summary>
public class PhysicalAttributes
{
    /// <summary>
    /// Relative height estimation (short, medium, tall)
    /// </summary>
    public string HeightCategory { get; set; } = string.Empty;

    /// <summary>
    /// Estimated height in meters based on relative position in frame
    /// </summary>
    public float EstimatedHeightMeters { get; set; }

    /// <summary>
    /// Build estimation (slim, medium, heavy)
    /// </summary>
    public string BuildCategory { get; set; } = string.Empty;

    /// <summary>
    /// Aspect ratio (width/height) of bounding box
    /// </summary>
    public float AspectRatio { get; set; }

    /// <summary>
    /// Hair length approximation (short, medium, long)
    /// </summary>
    public string HairLength { get; set; } = string.Empty;

    /// <summary>
    /// All attribute labels for matching
    /// </summary>
    public List<string> AllAttributes { get; set; } = new();
}
