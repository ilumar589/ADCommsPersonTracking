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

    [Fact]
    public void DetectAccessoriesFromYolo_WithOverlappingBackpack_ShouldAssociateBackpack()
    {
        // Arrange
        var personBox = new BoundingBox { X = 100, Y = 100, Width = 120, Height = 280, Confidence = 0.9f, Label = "person" };
        var allAccessories = new List<DetectedObject>
        {
            new DetectedObject
            {
                ClassId = 24,
                ObjectType = "backpack",
                BoundingBox = new BoundingBox { X = 150, Y = 120, Width = 60, Height = 80, Confidence = 0.85f, Label = "backpack" }
            }
        };

        // Act
        var result = _service.DetectAccessoriesFromYolo(personBox, allAccessories);

        // Assert
        result.Should().NotBeNull();
        result.Accessories.Should().HaveCount(1);
        result.Accessories[0].Label.Should().Be("backpack");
        result.Accessories[0].Confidence.Should().Be(0.85f);
    }

    [Fact]
    public void DetectAccessoriesFromYolo_WithMultipleAccessories_ShouldAssociateAll()
    {
        // Arrange
        var personBox = new BoundingBox { X = 100, Y = 100, Width = 120, Height = 280, Confidence = 0.9f, Label = "person" };
        var allAccessories = new List<DetectedObject>
        {
            new DetectedObject
            {
                ClassId = 24,
                ObjectType = "backpack",
                BoundingBox = new BoundingBox { X = 150, Y = 120, Width = 60, Height = 80, Confidence = 0.85f, Label = "backpack" }
            },
            new DetectedObject
            {
                ClassId = 26,
                ObjectType = "handbag",
                BoundingBox = new BoundingBox { X = 110, Y = 200, Width = 40, Height = 50, Confidence = 0.75f, Label = "handbag" }
            }
        };

        // Act
        var result = _service.DetectAccessoriesFromYolo(personBox, allAccessories);

        // Assert
        result.Should().NotBeNull();
        result.Accessories.Should().HaveCount(2);
        result.Accessories.Should().Contain(a => a.Label == "backpack");
        result.Accessories.Should().Contain(a => a.Label == "handbag");
    }

    [Fact]
    public void DetectAccessoriesFromYolo_WithDistantAccessory_ShouldNotAssociate()
    {
        // Arrange
        var personBox = new BoundingBox { X = 100, Y = 100, Width = 120, Height = 280, Confidence = 0.9f, Label = "person" };
        var allAccessories = new List<DetectedObject>
        {
            new DetectedObject
            {
                ClassId = 24,
                ObjectType = "backpack",
                BoundingBox = new BoundingBox { X = 500, Y = 500, Width = 60, Height = 80, Confidence = 0.85f, Label = "backpack" }
            }
        };

        // Act
        var result = _service.DetectAccessoriesFromYolo(personBox, allAccessories);

        // Assert
        result.Should().NotBeNull();
        result.Accessories.Should().BeEmpty();
    }

    [Fact]
    public void DetectAccessoriesFromYolo_WithTie_ShouldAddAsClothingItem()
    {
        // Arrange
        var personBox = new BoundingBox { X = 100, Y = 100, Width = 120, Height = 280, Confidence = 0.9f, Label = "person" };
        var allAccessories = new List<DetectedObject>
        {
            new DetectedObject
            {
                ClassId = 27,
                ObjectType = "tie",
                BoundingBox = new BoundingBox { X = 150, Y = 120, Width = 20, Height = 60, Confidence = 0.80f, Label = "tie" }
            }
        };

        // Act
        var result = _service.DetectAccessoriesFromYolo(personBox, allAccessories);

        // Assert
        result.Should().NotBeNull();
        result.ClothingItems.Should().HaveCount(1);
        result.ClothingItems[0].Label.Should().Be("tie");
    }

    [Fact]
    public void DetectAccessoriesFromYolo_WithNoAccessories_ShouldReturnEmpty()
    {
        // Arrange
        var personBox = new BoundingBox { X = 100, Y = 100, Width = 120, Height = 280, Confidence = 0.9f, Label = "person" };
        var allAccessories = new List<DetectedObject>();

        // Act
        var result = _service.DetectAccessoriesFromYolo(personBox, allAccessories);

        // Assert
        result.Should().NotBeNull();
        result.Accessories.Should().BeEmpty();
        result.ClothingItems.Should().BeEmpty();
    }

    [Fact]
    public void DetectAccessoriesFromYolo_WithBackpackBehindPerson_ShouldAssociate()
    {
        // Arrange - backpack slightly behind/overlapping person
        var personBox = new BoundingBox { X = 100, Y = 100, Width = 120, Height = 280, Confidence = 0.9f, Label = "person" };
        var allAccessories = new List<DetectedObject>
        {
            new DetectedObject
            {
                ClassId = 24,
                ObjectType = "backpack",
                BoundingBox = new BoundingBox { X = 90, Y = 120, Width = 50, Height = 70, Confidence = 0.85f, Label = "backpack" }
            }
        };

        // Act
        var result = _service.DetectAccessoriesFromYolo(personBox, allAccessories);

        // Assert
        result.Should().NotBeNull();
        result.Accessories.Should().HaveCount(1);
        result.Accessories[0].Label.Should().Be("backpack");
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
