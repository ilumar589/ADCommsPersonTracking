using ADCommsPersonTracking.Web.Models;

namespace ADCommsPersonTracking.Web.Services;

public interface IPersonTrackingApiService
{
    Task<HealthResponse?> GetHealthAsync();
    Task<TrackingResponse?> SubmitTrackingRequestAsync(TrackingRequest request);
    Task<List<PersonTrack>?> GetActiveTracksAsync();
    Task<PersonTrack?> GetTrackByIdAsync(string trackingId);
    Task<VideoUploadResponse?> UploadVideoAsync(Stream videoStream, string fileName);
    Task<TrackingResponse?> TrackByIdAsync(TrackByIdRequest request);
    Task<List<string>?> GetTrackingIdsAsync();
}
