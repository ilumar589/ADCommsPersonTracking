using ADCommsPersonTracking.Api.Models;
using ADCommsPersonTracking.Api.Services;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace ADCommsPersonTracking.Tests.Services;

public class AccessoryDetectionServiceTests
{
    private readonly AccessoryDetectionService _service;
    private readonly Mock<IConfiguration> _configurationMock;

    public AccessoryDetectionServiceTests()
    {
        _configurationMock = new Mock<IConfiguration>();
        _configurationMock.Setup(c => c["AccessoryDetection:ModelPath"]).Returns((string?)null);
        _configurationMock.Setup(c => c.GetSection("AccessoryDetection:ConfidenceThreshold").Value).Returns("0.5");
        
        var logger = Mock.Of<ILogger<AccessoryDetectionService>>();
        _service = new AccessoryDetectionService(_configurationMock.Object, logger);
    }

    [Fact]
    public async Task DetectAccessoriesAsync_ShouldReturnResult()
    {
        // Arrange
        var imageBytes = CreateTestImage(640, 480);
        var personBox = new BoundingBox { X = 100, Y = 100, Width = 120, Height = 280, Confidence = 0.9f };

        // Act
        var result = await _service.DetectAccessoriesAsync(imageBytes, personBox);

        // Assert
        result.Should().NotBeNull();
        result.Accessories.Should().NotBeNull();
        result.ClothingItems.Should().NotBeNull();
    }

    [Fact]
    public void MatchesCriteria_WithNoCriteria_ShouldReturnTrue()
    {
        // Arrange
        var detectionResult = new AccessoryDetectionResult
        {
            Accessories = new List<DetectedItem>
            {
                new DetectedItem("backpack", 0.8f)
            }
        };

        // Act
        var result = _service.MatchesCriteria(detectionResult, new List<string>(), new List<string>());

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void MatchesCriteria_WithMatchingClothing_ShouldReturnTrue()
    {
        // Arrange
        var detectionResult = new AccessoryDetectionResult
        {
            ClothingItems = new List<DetectedItem>
            {
                new DetectedItem("jacket", 0.85f),
                new DetectedItem("jeans", 0.75f)
            }
        };

        // Act
        var result = _service.MatchesCriteria(detectionResult, new List<string> { "jacket" }, new List<string>());

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void MatchesCriteria_WithMatchingAccessory_ShouldReturnTrue()
    {
        // Arrange
        var detectionResult = new AccessoryDetectionResult
        {
            Accessories = new List<DetectedItem>
            {
                new DetectedItem("backpack", 0.8f),
                new DetectedItem("hat", 0.7f)
            }
        };

        // Act
        var result = _service.MatchesCriteria(detectionResult, new List<string>(), new List<string> { "backpack" });

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void MatchesCriteria_WithPartialMatch_ShouldReturnTrue()
    {
        // Arrange
        var detectionResult = new AccessoryDetectionResult
        {
            ClothingItems = new List<DetectedItem>
            {
                new DetectedItem("winter jacket", 0.85f)
            }
        };

        // Act - "jacket" should match "winter jacket"
        var result = _service.MatchesCriteria(detectionResult, new List<string> { "jacket" }, new List<string>());

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void MatchesCriteria_WithNoMatch_ShouldReturnFalse()
    {
        // Arrange
        var detectionResult = new AccessoryDetectionResult
        {
            ClothingItems = new List<DetectedItem>
            {
                new DetectedItem("jacket", 0.85f)
            },
            Accessories = new List<DetectedItem>
            {
                new DetectedItem("backpack", 0.8f)
            }
        };

        // Act
        var result = _service.MatchesCriteria(detectionResult, new List<string> { "dress" }, new List<string> { "umbrella" });

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void MatchesCriteria_WithCaseInsensitiveMatch_ShouldReturnTrue()
    {
        // Arrange
        var detectionResult = new AccessoryDetectionResult
        {
            ClothingItems = new List<DetectedItem>
            {
                new DetectedItem("JACKET", 0.85f)
            }
        };

        // Act
        var result = _service.MatchesCriteria(detectionResult, new List<string> { "jacket" }, new List<string>());

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task DetectAccessoriesAsync_WithInvalidBox_ShouldReturnEmptyResult()
    {
        // Arrange
        var imageBytes = CreateTestImage(640, 480);
        var personBox = new BoundingBox { X = 1000, Y = 1000, Width = 100, Height = 200, Confidence = 0.9f };

        // Act
        var result = await _service.DetectAccessoriesAsync(imageBytes, personBox);

        // Assert
        result.Should().NotBeNull();
        result.Accessories.Should().BeEmpty();
        result.ClothingItems.Should().BeEmpty();
    }

    [Fact]
    public void DetectedItem_Constructor_ShouldSetProperties()
    {
        // Act
        var item = new DetectedItem("backpack", 0.85f);

        // Assert
        item.Label.Should().Be("backpack");
        item.Confidence.Should().Be(0.85f);
    }

    [Fact]
    public void DetectedItem_DefaultConstructor_ShouldCreateInstance()
    {
        // Act
        var item = new DetectedItem();

        // Assert
        item.Should().NotBeNull();
        item.Label.Should().Be(string.Empty);
        item.Confidence.Should().Be(0f);
    }

    private byte[] CreateTestImage(int width, int height)
    {
        using var image = new Image<Rgb24>(width, height);

        // Create an image with dark top (possible hat) and varied colors
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (y < height * 0.15)
                {
                    // Dark top region
                    image[x, y] = new Rgb24(30, 30, 30);
                }
                else
                {
                    // Varied middle region
                    image[x, y] = new Rgb24((byte)(x % 256), (byte)(y % 256), 128);
                }
            }
        }

        using var memoryStream = new MemoryStream();
        image.SaveAsJpeg(memoryStream);
        return memoryStream.ToArray();
    }
}
