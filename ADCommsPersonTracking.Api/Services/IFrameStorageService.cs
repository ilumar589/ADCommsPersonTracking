namespace ADCommsPersonTracking.Api.Services;

public interface IFrameStorageService
{
    Task<string> UploadFramesAsync(string trackingId, List<byte[]> frames);
    Task<List<byte[]>> GetFramesAsync(string trackingId);
    Task<bool> FramesExistAsync(string trackingId);
    Task<List<string>> GetAllTrackingIdsAsync();
}
