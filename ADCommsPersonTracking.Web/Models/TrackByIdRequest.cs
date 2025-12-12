namespace ADCommsPersonTracking.Web.Models;

public class TrackByIdRequest
{
    public string TrackingId { get; set; } = string.Empty;
    public string Prompt { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
}
