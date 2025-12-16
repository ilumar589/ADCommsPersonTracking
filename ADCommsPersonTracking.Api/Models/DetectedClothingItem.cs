namespace ADCommsPersonTracking.Api.Models;

/// <summary>
/// Represents a clothing item detected by the fashion YOLO model with its bounding box.
/// </summary>
public class DetectedClothingItem
{
    /// <summary>
    /// Clothing item label (e.g., "shirt", "jacket", "pants")
    /// </summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>
    /// Detection confidence score (0.0 to 1.0)
    /// </summary>
    public float Confidence { get; set; }

    /// <summary>
    /// Bounding box of the clothing item relative to the person's cropped image
    /// </summary>
    public BoundingBox BoundingBox { get; set; } = new();

    public DetectedClothingItem() { }

    public DetectedClothingItem(string label, float confidence, BoundingBox boundingBox)
    {
        Label = label;
        Confidence = confidence;
        BoundingBox = boundingBox;
    }
}

/// <summary>
/// Represents a clothing item with its detected colors.
/// Used to match queries like "blue jacket" or "red shirt".
/// </summary>
public class ClothingWithColors
{
    /// <summary>
    /// The detected clothing item with its bounding box
    /// </summary>
    public DetectedClothingItem ClothingItem { get; set; } = new();

    /// <summary>
    /// Colors detected specifically on this clothing item's region
    /// </summary>
    public List<DetectedColor> Colors { get; set; } = new();
}
