using ADCommsPersonTracking.Api.Models;

namespace ADCommsPersonTracking.Api.Services;

public interface IPersonTrackingService
{
    Task<TrackingResponse> ProcessFrameAsync(TrackingRequest request);
    Task<List<PersonTrack>> GetActiveTracksAsync();
    Task<PersonTrack?> GetTrackByIdAsync(string trackingId);
}
