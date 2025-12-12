using ADCommsPersonTracking.Api.Models;
using ADCommsPersonTracking.Api.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace ADCommsPersonTracking.Tests.Services;

public class PhysicalAttributeEstimatorTests
{
    private readonly PhysicalAttributeEstimator _estimator;

    public PhysicalAttributeEstimatorTests()
    {
        var logger = Mock.Of<ILogger<PhysicalAttributeEstimator>>();
        _estimator = new PhysicalAttributeEstimator(logger);
    }

    [Fact]
    public async Task EstimateAttributesAsync_ShouldReturnAttributes()
    {
        // Arrange
        var imageBytes = CreateTestImage(640, 480);
        var personBox = new BoundingBox { X = 100, Y = 50, Width = 120, Height = 300, Confidence = 0.9f };

        // Act
        var result = await _estimator.EstimateAttributesAsync(imageBytes, personBox, 480, 640);

        // Assert
        result.Should().NotBeNull();
        result.HeightCategory.Should().NotBeNullOrEmpty();
        result.BuildCategory.Should().NotBeNullOrEmpty();
        result.HairLength.Should().NotBeNullOrEmpty();
        result.AllAttributes.Should().NotBeEmpty();
    }

    [Fact]
    public async Task EstimateAttributesAsync_WithTallPerson_ShouldReturnTallCategory()
    {
        // Arrange
        var imageBytes = CreateTestImage(640, 480);
        // Person takes up 70% of image height
        var personBox = new BoundingBox { X = 200, Y = 0, Width = 100, Height = 336, Confidence = 0.9f };

        // Act
        var result = await _estimator.EstimateAttributesAsync(imageBytes, personBox, 480, 640);

        // Assert
        result.HeightCategory.Should().Be("tall");
    }

    [Fact]
    public async Task EstimateAttributesAsync_WithShortPerson_ShouldReturnShortCategory()
    {
        // Arrange
        var imageBytes = CreateTestImage(640, 480);
        // Person takes up 30% of image height
        var personBox = new BoundingBox { X = 200, Y = 200, Width = 100, Height = 144, Confidence = 0.9f };

        // Act
        var result = await _estimator.EstimateAttributesAsync(imageBytes, personBox, 480, 640);

        // Assert
        result.HeightCategory.Should().Be("short");
    }

    [Fact]
    public async Task EstimateAttributesAsync_WithSlimPerson_ShouldReturnSlimBuild()
    {
        // Arrange
        var imageBytes = CreateTestImage(640, 480);
        // Slim person: narrow width relative to height (aspect ratio < 0.35)
        var personBox = new BoundingBox { X = 250, Y = 100, Width = 80, Height = 250, Confidence = 0.9f };

        // Act
        var result = await _estimator.EstimateAttributesAsync(imageBytes, personBox, 480, 640);

        // Assert
        result.BuildCategory.Should().Be("slim");
        result.AspectRatio.Should().BeLessThan(0.35f);
    }

    [Fact]
    public async Task EstimateAttributesAsync_WithHeavyPerson_ShouldReturnHeavyBuild()
    {
        // Arrange
        var imageBytes = CreateTestImage(640, 480);
        // Heavy person: wide width relative to height (aspect ratio > 0.50)
        var personBox = new BoundingBox { X = 200, Y = 100, Width = 140, Height = 250, Confidence = 0.9f };

        // Act
        var result = await _estimator.EstimateAttributesAsync(imageBytes, personBox, 480, 640);

        // Assert
        result.BuildCategory.Should().Be("heavy");
        result.AspectRatio.Should().BeGreaterThan(0.50f);
    }

    [Fact]
    public void MatchesCriteria_WithNoCriteria_ShouldReturnTrue()
    {
        // Arrange
        var attributes = new PhysicalAttributes
        {
            HeightCategory = "tall",
            BuildCategory = "slim"
        };

        // Act
        var result = _estimator.MatchesCriteria(attributes, new List<string>(), null);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void MatchesCriteria_WithMatchingHeightKeyword_ShouldReturnTrue()
    {
        // Arrange
        var attributes = new PhysicalAttributes
        {
            HeightCategory = "tall",
            AllAttributes = new List<string> { "tall", "slim", "short hair" }
        };

        // Act
        var result = _estimator.MatchesCriteria(attributes, new List<string> { "tall" }, null);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void MatchesCriteria_WithMatchingBuildSynonym_ShouldReturnTrue()
    {
        // Arrange
        var attributes = new PhysicalAttributes
        {
            BuildCategory = "slim",
            AllAttributes = new List<string> { "medium height", "slim", "medium hair" }
        };

        // Act - "thin" is a synonym for "slim"
        var result = _estimator.MatchesCriteria(attributes, new List<string> { "thin" }, null);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void MatchesCriteria_WithNonMatchingAttribute_ShouldReturnFalse()
    {
        // Arrange
        var attributes = new PhysicalAttributes
        {
            HeightCategory = "tall",
            BuildCategory = "slim",
            AllAttributes = new List<string> { "tall", "slim", "medium hair" }
        };

        // Act - "heavy" does not match any attribute
        var result = _estimator.MatchesCriteria(attributes, new List<string> { "heavy" }, null);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void MatchesCriteria_WithMatchingHeight_ShouldReturnTrue()
    {
        // Arrange
        var attributes = new PhysicalAttributes
        {
            EstimatedHeightMeters = 1.75f
        };
        var searchHeight = new HeightInfo(1.78f, "1.78m"); // Within 10cm tolerance

        // Act
        var result = _estimator.MatchesCriteria(attributes, new List<string>(), searchHeight);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void MatchesCriteria_WithNonMatchingHeight_ShouldReturnFalse()
    {
        // Arrange
        var attributes = new PhysicalAttributes
        {
            EstimatedHeightMeters = 1.60f
        };
        var searchHeight = new HeightInfo(1.85f, "1.85m"); // More than 10cm difference

        // Act
        var result = _estimator.MatchesCriteria(attributes, new List<string>(), searchHeight);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task EstimateAttributesAsync_WithInvalidBoundingBox_ShouldReturnEmptyAttributes()
    {
        // Arrange
        var imageBytes = CreateTestImage(640, 480);
        // Box outside image bounds
        var personBox = new BoundingBox { X = 1000, Y = 1000, Width = 100, Height = 200, Confidence = 0.9f };

        // Act
        var result = await _estimator.EstimateAttributesAsync(imageBytes, personBox, 480, 640);

        // Assert
        result.Should().NotBeNull();
        result.HairLength.Should().Be("unknown");
    }

    private byte[] CreateTestImage(int width, int height)
    {
        using var image = new Image<Rgb24>(width, height);

        // Fill with gradient for testing
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                var brightness = (byte)((y * 255) / height);
                image[x, y] = new Rgb24(brightness, brightness, brightness);
            }
        }

        using var memoryStream = new MemoryStream();
        image.SaveAsJpeg(memoryStream);
        return memoryStream.ToArray();
    }
}
