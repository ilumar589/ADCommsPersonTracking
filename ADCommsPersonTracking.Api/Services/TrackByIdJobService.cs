using System.Collections.Concurrent;
using ADCommsPersonTracking.Api.Models;

namespace ADCommsPersonTracking.Api.Services;

public class TrackByIdJobService : ITrackByIdJobService
{
    private readonly ConcurrentDictionary<string, TrackByIdJob> _jobs = new();
    private readonly ILogger<TrackByIdJobService> _logger;

    public TrackByIdJobService(ILogger<TrackByIdJobService> logger)
    {
        _logger = logger;
    }

    public TrackByIdJob CreateJob(int totalFrames)
    {
        var job = new TrackByIdJob
        {
            JobId = Guid.NewGuid().ToString(),
            Status = "Pending",
            ProgressPercentage = 0,
            CurrentStep = "Initializing",
            CreatedAt = DateTime.UtcNow,
            TotalFrames = totalFrames,
            ProcessedFrames = 0
        };

        _jobs[job.JobId] = job;
        _logger.LogInformation("Created track-by-id job {JobId} for {TotalFrames} frames", job.JobId, totalFrames);
        return job;
    }

    public void UpdateProgress(string jobId, int processedFrames, string step)
    {
        if (_jobs.TryGetValue(jobId, out var job))
        {
            job.ProcessedFrames = processedFrames;
            job.CurrentStep = step;
            job.Status = "Processing";
            
            // Calculate percentage based on frames processed
            if (job.TotalFrames > 0)
            {
                var percentage = (int)((processedFrames / (double)job.TotalFrames) * 100);
                // Clamp percentage to 100 to prevent edge case overflow
                job.ProgressPercentage = Math.Min(percentage, 100);
            }
            
            _logger.LogInformation("Updated job {JobId}: {ProcessedFrames}/{TotalFrames} frames ({Percentage}%) - {Step}", 
                jobId, processedFrames, job.TotalFrames, job.ProgressPercentage, step);
        }
    }

    public void CompleteJob(string jobId, TrackingResponse results)
    {
        if (_jobs.TryGetValue(jobId, out var job))
        {
            job.Status = "Completed";
            job.ProgressPercentage = 100;
            job.CurrentStep = "Completed";
            job.TrackingResponse = results;
            job.CompletedAt = DateTime.UtcNow;
            _logger.LogInformation("Completed job {JobId}", jobId);
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

    public TrackByIdJob? GetJob(string jobId)
    {
        return _jobs.TryGetValue(jobId, out var job) ? job : null;
    }
}
