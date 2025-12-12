using ADCommsPersonTracking.Web.Models;

namespace ADCommsPersonTracking.Web.Services;

public interface IPersonTrackingApiService
{
    Task<HealthResponse?> GetHealthAsync();
    Task<TrackingResponse?> SubmitTrackingRequestAsync(TrackingRequest request);
    Task<List<PersonTrack>?> GetActiveTracksAsync();
    Task<PersonTrack?> GetTrackByIdAsync(string trackingId);
}
