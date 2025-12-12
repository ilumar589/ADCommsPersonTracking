using ADCommsPersonTracking.Api.Models;

namespace ADCommsPersonTracking.Api.Services;

/// <summary>
/// Service for detecting accessories and clothing items on persons.
/// </summary>
public interface IAccessoryDetectionService
{
    /// <summary>
    /// Detect accessories and clothing items for a person.
    /// </summary>
    /// <param name="imageBytes">The full image bytes</param>
    /// <param name="personBox">The bounding box of the person</param>
    /// <returns>Detected accessories and clothing items</returns>
    Task<AccessoryDetectionResult> DetectAccessoriesAsync(byte[] imageBytes, BoundingBox personBox);

    /// <summary>
    /// Check if detected items match the search criteria.
    /// </summary>
    /// <param name="detectionResult">The detection result</param>
    /// <param name="searchClothing">Search criteria for clothing items</param>
    /// <param name="searchAccessories">Search criteria for accessories</param>
    /// <returns>True if items match, false otherwise</returns>
    bool MatchesCriteria(AccessoryDetectionResult detectionResult, List<string> searchClothing, List<string> searchAccessories);
}

/// <summary>
/// Result of accessory and clothing detection.
/// </summary>
public class AccessoryDetectionResult
{
    /// <summary>
    /// Detected accessories (backpack, handbag, hat, glasses, etc.)
    /// </summary>
    public List<DetectedItem> Accessories { get; set; } = new();

    /// <summary>
    /// Detected clothing items (jacket, dress, shorts, etc.)
    /// </summary>
    public List<DetectedItem> ClothingItems { get; set; } = new();
}

/// <summary>
/// A detected item with confidence score.
/// </summary>
public class DetectedItem
{
    /// <summary>
    /// Item name/label
    /// </summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>
    /// Detection confidence
    /// </summary>
    public float Confidence { get; set; }

    public DetectedItem() { }

    public DetectedItem(string label, float confidence)
    {
        Label = label;
        Confidence = confidence;
    }
}
