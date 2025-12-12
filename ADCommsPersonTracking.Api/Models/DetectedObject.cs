namespace ADCommsPersonTracking.Api.Models;

/// <summary>
/// Represents an object detected by YOLO with its bounding box and classification.
/// </summary>
public class DetectedObject
{
    /// <summary>
    /// Bounding box of the detected object.
    /// </summary>
    public BoundingBox BoundingBox { get; set; } = new();

    /// <summary>
    /// COCO class index (0=person, 24=backpack, 26=handbag, 28=suitcase, etc.)
    /// </summary>
    public int ClassId { get; set; }

    /// <summary>
    /// Object type (e.g., "person", "backpack", "handbag")
    /// </summary>
    public string ObjectType { get; set; } = string.Empty;
}
