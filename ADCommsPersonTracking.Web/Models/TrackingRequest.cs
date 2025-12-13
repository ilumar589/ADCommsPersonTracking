namespace ADCommsPersonTracking.Web.Models;

public class TrackingRequest
{
    public List<string> ImagesBase64 { get; set; } = new();
    public string Prompt { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string? DiagnosticsSessionId { get; set; }
}
