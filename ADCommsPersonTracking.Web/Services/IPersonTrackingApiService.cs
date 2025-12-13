using ADCommsPersonTracking.Web.Models;

namespace ADCommsPersonTracking.Web.Services;

public interface IPersonTrackingApiService
{
    Task<HealthResponse?> GetHealthAsync();
    Task<TrackingResponse?> SubmitTrackingRequestAsync(TrackingRequest request);
    Task<List<PersonTrack>?> GetActiveTracksAsync();
    Task<PersonTrack?> GetTrackByIdAsync(string trackingId);
    Task<VideoUploadJobResponse?> UploadVideoAsync(Stream videoStream, string fileName);
    Task<VideoUploadJobStatus?> GetVideoUploadStatusAsync(string jobId);
    Task<TrackingResponse?> TrackByIdAsync(TrackByIdRequest request);
    Task<List<string>?> GetTrackingIdsAsync();
}
