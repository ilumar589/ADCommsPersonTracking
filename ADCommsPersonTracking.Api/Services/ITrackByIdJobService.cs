using ADCommsPersonTracking.Api.Models;

namespace ADCommsPersonTracking.Api.Services;

public interface ITrackByIdJobService
{
    TrackByIdJob CreateJob(int totalFrames);
    void UpdateProgress(string jobId, int processedFrames, string step);
    void CompleteJob(string jobId, TrackingResponse results);
    void FailJob(string jobId, string error);
    TrackByIdJob? GetJob(string jobId);
}
