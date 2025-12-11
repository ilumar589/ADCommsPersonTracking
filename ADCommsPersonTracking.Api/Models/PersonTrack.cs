namespace ADCommsPersonTracking.Api.Models;

public class PersonTrack
{
    public string TrackingId { get; set; } = string.Empty;
    public string CameraId { get; set; } = string.Empty;
    public DateTime FirstSeen { get; set; }
    public DateTime LastSeen { get; set; }
    public BoundingBox LastKnownPosition { get; set; } = new();
    public string Description { get; set; } = string.Empty;
    public List<string> Features { get; set; } = new();
}
