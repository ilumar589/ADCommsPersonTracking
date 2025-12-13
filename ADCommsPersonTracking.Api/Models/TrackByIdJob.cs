namespace ADCommsPersonTracking.Api.Models;

public class TrackByIdJob
{
    public string JobId { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending"; // Pending, Processing, Completed, Failed
    public int ProgressPercentage { get; set; }
    public string CurrentStep { get; set; } = string.Empty;
    public TrackingResponse? TrackingResponse { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int TotalFrames { get; set; }
    public int ProcessedFrames { get; set; }
}
