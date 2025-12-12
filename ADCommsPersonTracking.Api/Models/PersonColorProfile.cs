namespace ADCommsPersonTracking.Api.Models;

public class PersonColorProfile
{
    public List<DetectedColor> UpperBodyColors { get; set; } = new();
    public List<DetectedColor> LowerBodyColors { get; set; } = new();
    public List<DetectedColor> OverallColors { get; set; } = new();
}

public readonly record struct DetectedColor(string ColorName, float Confidence, string HexValue);
