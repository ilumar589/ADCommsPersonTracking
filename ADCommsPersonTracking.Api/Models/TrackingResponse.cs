namespace ADCommsPersonTracking.Api.Models;

public class TrackingResponse
{
    public string CameraId { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public List<Detection> Detections { get; set; } = new();
    public string ProcessingMessage { get; set; } = string.Empty;
    public string AnnotatedImageBase64 { get; set; } = string.Empty;
}

public class Detection
{
    public string TrackingId { get; set; } = string.Empty;
    public BoundingBox BoundingBox { get; set; } = new();
    public string Description { get; set; } = string.Empty;
    public float MatchScore { get; set; }
}
