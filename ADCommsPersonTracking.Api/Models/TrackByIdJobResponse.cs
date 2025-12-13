namespace ADCommsPersonTracking.Api.Models;

public class TrackByIdJobResponse
{
    public string JobId { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public int TotalFrames { get; set; }
}
