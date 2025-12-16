using ADCommsPersonTracking.Api.Models;

namespace ADCommsPersonTracking.Api.Services;

/// <summary>
/// Service for detecting clothing items on persons using a fashion-trained YOLO model.
/// </summary>
public interface IClothingDetectionService
{
    /// <summary>
    /// Detect clothing items on a cropped person image.
    /// Returns clothing items with bounding boxes for color analysis.
    /// </summary>
    /// <param name="imageBytes">The cropped person image bytes</param>
    /// <param name="confidenceThreshold">Optional confidence threshold override</param>
    /// <returns>List of detected clothing items with bounding boxes and confidence scores</returns>
    Task<List<DetectedClothingItem>> DetectClothingAsync(byte[] imageBytes, float? confidenceThreshold = null);
}
