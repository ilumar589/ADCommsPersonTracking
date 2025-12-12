namespace ADCommsPersonTracking.Api.Models;

public class SearchCriteria
{
    public List<string> Colors { get; set; } = new();
    public List<string> ClothingItems { get; set; } = new();
    public List<string> Accessories { get; set; } = new();
    public HeightInfo? Height { get; set; }
    public List<string> PhysicalAttributes { get; set; } = new();
}

public readonly record struct HeightInfo(float Meters, string OriginalText);
