namespace ADCommsPersonTracking.Web.Models;

public class VideoUploadResponse
{
    public string TrackingId { get; set; } = string.Empty;
    public int FrameCount { get; set; }
    public bool WasCached { get; set; }
    public string Message { get; set; } = string.Empty;
}

public class VideoUploadJobResponse
{
    public string JobId { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

public class VideoUploadJobStatus
{
    public string JobId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int ProgressPercentage { get; set; }
    public string CurrentStep { get; set; } = string.Empty;
    public string? TrackingId { get; set; }
    public int? FrameCount { get; set; }
    public string? ErrorMessage { get; set; }
}
