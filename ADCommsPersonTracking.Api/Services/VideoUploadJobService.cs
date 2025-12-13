using System.Collections.Concurrent;
using ADCommsPersonTracking.Api.Models;

namespace ADCommsPersonTracking.Api.Services;

public class VideoUploadJobService : IVideoUploadJobService
{
    private readonly ConcurrentDictionary<string, VideoUploadJob> _jobs = new();
    private readonly ILogger<VideoUploadJobService> _logger;

    public VideoUploadJobService(ILogger<VideoUploadJobService> logger)
    {
        _logger = logger;
    }

    public VideoUploadJob CreateJob()
    {
        var job = new VideoUploadJob
        {
            JobId = Guid.NewGuid().ToString(),
            Status = "Pending",
            ProgressPercentage = 0,
            CurrentStep = "Initializing",
            CreatedAt = DateTime.UtcNow
        };

        _jobs[job.JobId] = job;
        _logger.LogInformation("Created video upload job {JobId}", job.JobId);
        return job;
    }

    public void UpdateProgress(string jobId, int percentage, string step)
    {
        if (_jobs.TryGetValue(jobId, out var job))
        {
            job.ProgressPercentage = percentage;
            job.CurrentStep = step;
            job.Status = "Processing";
            _logger.LogInformation("Updated job {JobId}: {Percentage}% - {Step}", jobId, percentage, step);
        }
    }

    public void CompleteJob(string jobId, string trackingId, int frameCount)
    {
        if (_jobs.TryGetValue(jobId, out var job))
        {
            job.Status = "Completed";
            job.ProgressPercentage = 100;
            job.CurrentStep = "Completed";
            job.TrackingId = trackingId;
            job.FrameCount = frameCount;
            job.CompletedAt = DateTime.UtcNow;
            _logger.LogInformation("Completed job {JobId} with tracking ID {TrackingId}", jobId, trackingId);
        }
    }

    public void FailJob(string jobId, string error)
    {
        if (_jobs.TryGetValue(jobId, out var job))
        {
            job.Status = "Failed";
            job.ErrorMessage = error;
            job.CompletedAt = DateTime.UtcNow;
            _logger.LogError("Failed job {JobId}: {Error}", jobId, error);
        }
    }

    public VideoUploadJob? GetJob(string jobId)
    {
        return _jobs.TryGetValue(jobId, out var job) ? job : null;
    }
}
