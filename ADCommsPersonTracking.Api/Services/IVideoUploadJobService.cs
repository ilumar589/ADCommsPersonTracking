using ADCommsPersonTracking.Api.Models;

namespace ADCommsPersonTracking.Api.Services;

public interface IVideoUploadJobService
{
    VideoUploadJob CreateJob();
    void UpdateProgress(string jobId, int percentage, string step);
    void CompleteJob(string jobId, string trackingId, int frameCount);
    void FailJob(string jobId, string error);
    VideoUploadJob? GetJob(string jobId);
}
