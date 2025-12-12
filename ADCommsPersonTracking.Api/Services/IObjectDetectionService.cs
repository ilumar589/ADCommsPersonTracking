using ADCommsPersonTracking.Api.Models;

namespace ADCommsPersonTracking.Api.Services;

public interface IObjectDetectionService
{
    Task<List<BoundingBox>> DetectPersonsAsync(byte[] imageBytes);
    
    /// <summary>
    /// Detects persons and accessories (backpack, handbag, suitcase, etc.) in an image.
    /// </summary>
    /// <param name="imageBytes">The image bytes to analyze</param>
    /// <returns>List of detected objects with their classifications and bounding boxes</returns>
    Task<List<DetectedObject>> DetectObjectsAsync(byte[] imageBytes);
}
