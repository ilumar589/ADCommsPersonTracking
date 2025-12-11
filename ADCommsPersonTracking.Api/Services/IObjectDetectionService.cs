using ADCommsPersonTracking.Api.Models;

namespace ADCommsPersonTracking.Api.Services;

public interface IObjectDetectionService
{
    Task<List<BoundingBox>> DetectPersonsAsync(byte[] imageBytes);
}
