namespace ADCommsPersonTracking.Api.Models;

public class PersonColorProfile
{
    public List<DetectedColor> UpperBodyColors { get; set; } = new();
    public List<DetectedColor> LowerBodyColors { get; set; } = new();
    public List<DetectedColor> OverallColors { get; set; } = new();
}

public class DetectedColor
{
    public string ColorName { get; set; } = string.Empty;
    public float Confidence { get; set; }
    public string HexValue { get; set; } = string.Empty;
}
