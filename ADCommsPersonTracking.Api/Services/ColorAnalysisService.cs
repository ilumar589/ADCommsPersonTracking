using ADCommsPersonTracking.Api.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace ADCommsPersonTracking.Api.Services;

public class ColorAnalysisService : IColorAnalysisService
{
    private readonly ILogger<ColorAnalysisService> _logger;

    // Define standard colors with their RGB values for matching
    private static readonly Dictionary<string, Rgb24> StandardColors = new()
    {
        { "red", new Rgb24(255, 0, 0) },
        { "blue", new Rgb24(0, 0, 255) },
        { "green", new Rgb24(0, 128, 0) },
        { "yellow", new Rgb24(255, 255, 0) },
        { "orange", new Rgb24(255, 165, 0) },
        { "purple", new Rgb24(128, 0, 128) },
        { "pink", new Rgb24(255, 192, 203) },
        { "black", new Rgb24(0, 0, 0) },
        { "white", new Rgb24(255, 255, 255) },
        { "gray", new Rgb24(128, 128, 128) },
        { "brown", new Rgb24(165, 42, 42) },
        { "beige", new Rgb24(245, 245, 220) }
    };

    public ColorAnalysisService(ILogger<ColorAnalysisService> logger)
    {
        _logger = logger;
    }

    public async Task<PersonColorProfile> AnalyzePersonColorsAsync(byte[] imageBytes, BoundingBox personBox)
    {
        try
        {
            using var memoryStream = new MemoryStream(imageBytes);
            using var image = await Image.LoadAsync<Rgb24>(memoryStream);
            
            // Ensure bounding box is within image bounds
            var x = Math.Max(0, (int)personBox.X);
            var y = Math.Max(0, (int)personBox.Y);
            var width = Math.Min((int)personBox.Width, image.Width - x);
            var height = Math.Min((int)personBox.Height, image.Height - y);

            if (width <= 0 || height <= 0)
            {
                _logger.LogWarning("Invalid bounding box dimensions for person");
                return new PersonColorProfile();
            }

            // Crop to person region
            var personRegion = image.Clone(ctx => ctx.Crop(new Rectangle(x, y, width, height)));

            // Analyze different body regions in parallel for better performance
            var analysisTask1 = AnalyzeRegionAsync(personRegion, 0, 0, width, (int)(height * 0.5), 3);
            var analysisTask2 = AnalyzeRegionAsync(personRegion, 0, (int)(height * 0.5), width, (int)(height * 0.5), 3);
            var analysisTask3 = AnalyzeRegionAsync(personRegion, 0, 0, width, height, 5);
            
            await Task.WhenAll(analysisTask1, analysisTask2, analysisTask3);
            
            var upperBodyColors = analysisTask1.Result;
            var lowerBodyColors = analysisTask2.Result;
            var overallColors = analysisTask3.Result;

            return new PersonColorProfile
            {
                UpperBodyColors = upperBodyColors,
                LowerBodyColors = lowerBodyColors,
                OverallColors = overallColors
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing person colors");
            return new PersonColorProfile();
        }
    }

    public bool MatchesColorCriteria(PersonColorProfile profile, List<string> searchColors)
    {
        if (searchColors == null || searchColors.Count == 0)
        {
            return true; // No color criteria, match all
        }

        // Check if any of the search colors are present in the person's color profile
        var allDetectedColors = profile.UpperBodyColors
            .Concat(profile.LowerBodyColors)
            .Concat(profile.OverallColors)
            .Select(c => c.ColorName.ToLowerInvariant())
            .Distinct()
            .ToList();

        foreach (var searchColor in searchColors)
        {
            if (allDetectedColors.Contains(searchColor.ToLowerInvariant()))
            {
                return true;
            }
        }

        return false;
    }

    private Task<List<DetectedColor>> AnalyzeRegionAsync(Image<Rgb24> image, int x, int y, int width, int height, int topN)
    {
        return Task.Run(() =>
        {
            var colorHistogram = new Dictionary<string, (int count, Rgb24 avgRgb)>();

            // Sample pixels to build histogram (every 5th pixel for efficiency)
            var step = 5;
            for (int py = y; py < y + height; py += step)
            {
                for (int px = x; px < x + width; px += step)
                {
                    if (px >= image.Width || py >= image.Height)
                        continue;

                    var pixel = image[px, py];
                    var colorName = GetClosestColorName(pixel);

                    if (!colorHistogram.ContainsKey(colorName))
                    {
                        colorHistogram[colorName] = (0, new Rgb24(0, 0, 0));
                    }

                    var current = colorHistogram[colorName];
                    // Compute average RGB values
                    var newCount = current.count + 1;
                    var avgR = (byte)((current.avgRgb.R * current.count + pixel.R) / newCount);
                    var avgG = (byte)((current.avgRgb.G * current.count + pixel.G) / newCount);
                    var avgB = (byte)((current.avgRgb.B * current.count + pixel.B) / newCount);
                    colorHistogram[colorName] = (newCount, new Rgb24(avgR, avgG, avgB));
                }
            }

            // Convert histogram to detected colors
            var totalPixels = colorHistogram.Values.Sum(v => v.count);
            var detectedColors = colorHistogram
                .Select(kvp => new DetectedColor
                {
                    ColorName = kvp.Key,
                    Confidence = (float)kvp.Value.count / totalPixels,
                    HexValue = ColorToHex(kvp.Value.avgRgb)
                })
                .OrderByDescending(c => c.Confidence)
                .Take(topN)
                .ToList();

            return detectedColors;
        });
    }

    private string GetClosestColorName(Rgb24 pixel)
    {
        // First check if it's grayscale
        var r = pixel.R;
        var g = pixel.G;
        var b = pixel.B;

        var maxDiff = Math.Max(Math.Abs(r - g), Math.Max(Math.Abs(g - b), Math.Abs(r - b)));
        if (maxDiff < 30)
        {
            // It's grayscale
            var brightness = (r + g + b) / 3;
            if (brightness < 50)
                return "black";
            else if (brightness > 200)
                return "white";
            else
                return "gray";
        }

        // Find closest color by Euclidean distance
        var minDistance = double.MaxValue;
        var closestColor = "gray";

        foreach (var color in StandardColors)
        {
            var distance = CalculateColorDistance(pixel, color.Value);
            if (distance < minDistance)
            {
                minDistance = distance;
                closestColor = color.Key;
            }
        }

        return closestColor;
    }

    private double CalculateColorDistance(Rgb24 color1, Rgb24 color2)
    {
        var rDiff = color1.R - color2.R;
        var gDiff = color1.G - color2.G;
        var bDiff = color1.B - color2.B;
        return Math.Sqrt(rDiff * rDiff + gDiff * gDiff + bDiff * bDiff);
    }

    private string ColorToHex(Rgb24 color)
    {
        return $"#{color.R:X2}{color.G:X2}{color.B:X2}";
    }
}
