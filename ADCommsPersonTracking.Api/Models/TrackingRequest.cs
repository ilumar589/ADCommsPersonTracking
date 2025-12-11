namespace ADCommsPersonTracking.Api.Models;

public class TrackingRequest
{
    public string CameraId { get; set; } = string.Empty;
    public string ImageBase64 { get; set; } = string.Empty;
    public string Prompt { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
}
