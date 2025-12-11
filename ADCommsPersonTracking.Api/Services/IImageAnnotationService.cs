using ADCommsPersonTracking.Api.Models;

namespace ADCommsPersonTracking.Api.Services;

public interface IImageAnnotationService
{
    Task<string> AnnotateImageAsync(byte[] imageBytes, List<BoundingBox> boundingBoxes);
}
