using ADCommsPersonTracking.Api.Models;
using ADCommsPersonTracking.Api.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace ADCommsPersonTracking.Tests.Services;

public class ColorAnalysisServiceTests
{
    private readonly ColorAnalysisService _service;

    public ColorAnalysisServiceTests()
    {
        var logger = Mock.Of<ILogger<ColorAnalysisService>>();
        _service = new ColorAnalysisService(logger);
    }

    [Fact]
    public async Task AnalyzePersonColorsAsync_WithRedImage_ReturnsRedColor()
    {
        // Arrange
        var imageBytes = CreateColoredImage(640, 480, new Rgb24(255, 0, 0)); // Red image
        var personBox = new BoundingBox
        {
            X = 100,
            Y = 100,
            Width = 200,
            Height = 300,
            Confidence = 0.9f,
            Label = "person"
        };

        // Act
        var result = await _service.AnalyzePersonColorsAsync(imageBytes, personBox);

        // Assert
        result.Should().NotBeNull();
        result.OverallColors.Should().NotBeEmpty();
        result.OverallColors.Should().Contain(c => c.ColorName == "red");
    }

    [Fact]
    public async Task AnalyzePersonColorsAsync_WithBlueImage_ReturnsBlueColor()
    {
        // Arrange
        var imageBytes = CreateColoredImage(640, 480, new Rgb24(0, 0, 255)); // Blue image
        var personBox = new BoundingBox
        {
            X = 100,
            Y = 100,
            Width = 200,
            Height = 300,
            Confidence = 0.9f,
            Label = "person"
        };

        // Act
        var result = await _service.AnalyzePersonColorsAsync(imageBytes, personBox);

        // Assert
        result.Should().NotBeNull();
        result.OverallColors.Should().NotBeEmpty();
        result.OverallColors.Should().Contain(c => c.ColorName == "blue");
    }

    [Fact]
    public async Task AnalyzePersonColorsAsync_ReturnsUpperAndLowerBodyColors()
    {
        // Arrange
        var imageBytes = CreateColoredImage(640, 480, new Rgb24(0, 255, 0)); // Green image
        var personBox = new BoundingBox
        {
            X = 100,
            Y = 100,
            Width = 200,
            Height = 300,
            Confidence = 0.9f,
            Label = "person"
        };

        // Act
        var result = await _service.AnalyzePersonColorsAsync(imageBytes, personBox);

        // Assert
        result.Should().NotBeNull();
        result.UpperBodyColors.Should().NotBeEmpty();
        result.LowerBodyColors.Should().NotBeEmpty();
        result.OverallColors.Should().NotBeEmpty();
    }

    [Fact]
    public void MatchesColorCriteria_WithMatchingColor_ReturnsTrue()
    {
        // Arrange
        var profile = new PersonColorProfile
        {
            OverallColors = new List<DetectedColor>
            {
                new DetectedColor { ColorName = "red", Confidence = 0.7f, HexValue = "#FF0000" },
                new DetectedColor { ColorName = "blue", Confidence = 0.3f, HexValue = "#0000FF" }
            }
        };
        var searchColors = new List<string> { "red" };

        // Act
        var result = _service.MatchesColorCriteria(profile, searchColors);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void MatchesColorCriteria_WithNonMatchingColor_ReturnsFalse()
    {
        // Arrange
        var profile = new PersonColorProfile
        {
            OverallColors = new List<DetectedColor>
            {
                new DetectedColor { ColorName = "red", Confidence = 0.7f, HexValue = "#FF0000" },
                new DetectedColor { ColorName = "blue", Confidence = 0.3f, HexValue = "#0000FF" }
            }
        };
        var searchColors = new List<string> { "green" };

        // Act
        var result = _service.MatchesColorCriteria(profile, searchColors);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void MatchesColorCriteria_WithNoSearchColors_ReturnsTrue()
    {
        // Arrange
        var profile = new PersonColorProfile
        {
            OverallColors = new List<DetectedColor>
            {
                new DetectedColor { ColorName = "red", Confidence = 0.7f, HexValue = "#FF0000" }
            }
        };
        var searchColors = new List<string>();

        // Act
        var result = _service.MatchesColorCriteria(profile, searchColors);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void MatchesColorCriteria_ChecksUpperBodyColors()
    {
        // Arrange
        var profile = new PersonColorProfile
        {
            UpperBodyColors = new List<DetectedColor>
            {
                new DetectedColor { ColorName = "green", Confidence = 0.7f, HexValue = "#00FF00" }
            },
            OverallColors = new List<DetectedColor>
            {
                new DetectedColor { ColorName = "green", Confidence = 0.5f, HexValue = "#00FF00" }
            }
        };
        var searchColors = new List<string> { "green" };

        // Act
        var result = _service.MatchesColorCriteria(profile, searchColors);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void MatchesColorCriteria_ChecksLowerBodyColors()
    {
        // Arrange
        var profile = new PersonColorProfile
        {
            LowerBodyColors = new List<DetectedColor>
            {
                new DetectedColor { ColorName = "blue", Confidence = 0.7f, HexValue = "#0000FF" }
            },
            OverallColors = new List<DetectedColor>
            {
                new DetectedColor { ColorName = "blue", Confidence = 0.5f, HexValue = "#0000FF" }
            }
        };
        var searchColors = new List<string> { "blue" };

        // Act
        var result = _service.MatchesColorCriteria(profile, searchColors);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task AnalyzePersonColorsAsync_WithInvalidBoundingBox_ReturnsEmptyProfile()
    {
        // Arrange
        var imageBytes = CreateColoredImage(640, 480, new Rgb24(255, 0, 0));
        var personBox = new BoundingBox
        {
            X = 1000, // Outside image bounds
            Y = 1000,
            Width = 200,
            Height = 300,
            Confidence = 0.9f,
            Label = "person"
        };

        // Act
        var result = await _service.AnalyzePersonColorsAsync(imageBytes, personBox);

        // Assert
        result.Should().NotBeNull();
        result.OverallColors.Should().BeEmpty();
    }

    private byte[] CreateColoredImage(int width, int height, Rgb24 color)
    {
        using var image = new Image<Rgb24>(width, height);
        
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                image[x, y] = color;
            }
        }

        using var memoryStream = new MemoryStream();
        image.SaveAsJpeg(memoryStream);
        return memoryStream.ToArray();
    }
}
