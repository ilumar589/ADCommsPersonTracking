namespace ADCommsPersonTracking.Api.Models;

public class VideoUploadResponse
{
    public string TrackingId { get; set; } = string.Empty;
    public int FrameCount { get; set; }
    public bool WasCached { get; set; }
    public string Message { get; set; } = string.Empty;
}
