namespace ADCommsPersonTracking.Api.Models;

public class VideoUploadJob
{
    public string JobId { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending"; // Pending, Processing, Completed, Failed
    public int ProgressPercentage { get; set; }
    public string CurrentStep { get; set; } = string.Empty;
    public string? TrackingId { get; set; }
    public int? FrameCount { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}
