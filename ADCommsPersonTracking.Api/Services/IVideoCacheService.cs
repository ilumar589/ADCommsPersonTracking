namespace ADCommsPersonTracking.Api.Services;

public interface IVideoCacheService
{
    Task<string?> GetTrackingIdByVideoNameAsync(string videoName);
    Task SetTrackingIdForVideoAsync(string videoName, string trackingId);
}
