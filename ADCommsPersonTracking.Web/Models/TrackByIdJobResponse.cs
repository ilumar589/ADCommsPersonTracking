namespace ADCommsPersonTracking.Web.Models;

public class TrackByIdJobResponse
{
    public string JobId { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public int TotalFrames { get; set; }
}

public class TrackByIdJobStatus
{
    public string JobId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int ProgressPercentage { get; set; }
    public string CurrentStep { get; set; } = string.Empty;
    public TrackingResponse? TrackingResponse { get; set; }
    public string? ErrorMessage { get; set; }
    public int TotalFrames { get; set; }
    public int ProcessedFrames { get; set; }
}
