using ADCommsPersonTracking.Web.Models;

namespace ADCommsPersonTracking.Web.Services;

public interface IPersonTrackingApiService
{
    Task<HealthResponse?> GetHealthAsync();
    Task<TrackingResponse?> SubmitTrackingRequestAsync(TrackingRequest request);
    Task<List<PersonTrack>?> GetActiveTracksAsync();
    Task<PersonTrack?> GetTrackByIdAsync(string trackingId);
    Task<VideoUploadJobResponse?> UploadVideoAsync(Stream videoStream, string fileName, int? maxFrames = null);
    Task<VideoUploadJobStatus?> GetVideoUploadStatusAsync(string jobId);
    Task<TrackByIdJobResponse?> TrackByIdAsync(TrackByIdRequest request);
    Task<TrackByIdJobStatus?> GetTrackByIdStatusAsync(string jobId);
    Task<List<string>?> GetTrackingIdsAsync();
    Task<TrackByIdWithDiagnosticsResponse?> TrackByIdWithDiagnosticsAsync(TrackByIdRequest request);
    Task<InferenceDiagnostics?> GetDiagnosticsAsync(string sessionId);
    Task<InferenceDiagnostics?> GetLatestDiagnosticsAsync();
}
