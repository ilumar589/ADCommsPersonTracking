using ADCommsPersonTracking.Api.Models;
using ADCommsPersonTracking.Api.Services;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace ADCommsPersonTracking.Tests.Services;

public class ImageAnnotationServiceTests
{
    private readonly ImageAnnotationService _service;

    public ImageAnnotationServiceTests()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ImageAnnotation:BoxColor"] = "#00FF00",
                ["ImageAnnotation:BoxThickness"] = "2",
                ["ImageAnnotation:ShowLabels"] = "true",
                ["ImageAnnotation:FontSize"] = "12"
            })
            .Build();

        var logger = Mock.Of<ILogger<ImageAnnotationService>>();
        _service = new ImageAnnotationService(configuration, logger);
    }

    [Fact]
    public async Task AnnotateImageAsync_WithEmptyDetectionList_ReturnsBase64Image()
    {
        // Arrange
        var imageBytes = CreateTestImage(640, 480);
        var detections = new List<BoundingBox>();

        // Act
        var result = await _service.AnnotateImageAsync(imageBytes, detections);

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().Match(s => IsValidBase64(s));
    }

    [Fact]
    public async Task AnnotateImageAsync_WithSingleDetection_ReturnsAnnotatedImage()
    {
        // Arrange
        var imageBytes = CreateTestImage(640, 480);
        var detections = new List<BoundingBox>
        {
            new BoundingBox
            {
                X = 100,
                Y = 150,
                Width = 120,
                Height = 280,
                Confidence = 0.85f,
                Label = "person"
            }
        };

        // Act
        var result = await _service.AnnotateImageAsync(imageBytes, detections);

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().Match(s => IsValidBase64(s));
        
        // Verify the annotated image is different from original
        var originalBase64 = Convert.ToBase64String(imageBytes);
        result.Should().NotBe(originalBase64);
    }

    [Fact]
    public async Task AnnotateImageAsync_WithMultipleDetections_ReturnsAnnotatedImage()
    {
        // Arrange
        var imageBytes = CreateTestImage(640, 480);
        var detections = new List<BoundingBox>
        {
            new BoundingBox
            {
                X = 100,
                Y = 150,
                Width = 120,
                Height = 280,
                Confidence = 0.85f,
                Label = "person"
            },
            new BoundingBox
            {
                X = 300,
                Y = 200,
                Width = 110,
                Height = 260,
                Confidence = 0.92f,
                Label = "person"
            },
            new BoundingBox
            {
                X = 450,
                Y = 180,
                Width = 115,
                Height = 270,
                Confidence = 0.78f,
                Label = "person"
            }
        };

        // Act
        var result = await _service.AnnotateImageAsync(imageBytes, detections);

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().Match(s => IsValidBase64(s));
    }

    [Fact]
    public async Task AnnotateImageAsync_WithInvalidImageBytes_ThrowsException()
    {
        // Arrange
        var invalidBytes = new byte[] { 1, 2, 3, 4, 5 };
        var detections = new List<BoundingBox>();

        // Act & Assert
        await FluentActions.Invoking(async () =>
                await _service.AnnotateImageAsync(invalidBytes, detections))
            .Should().ThrowAsync<Exception>();
    }

    [Fact]
    public async Task AnnotateImageAsync_WithDifferentImageSizes_WorksCorrectly()
    {
        // Test with small image
        var smallImage = CreateTestImage(320, 240);
        var detections = new List<BoundingBox>
        {
            new BoundingBox { X = 50, Y = 60, Width = 80, Height = 120, Confidence = 0.9f, Label = "person" }
        };
        var result1 = await _service.AnnotateImageAsync(smallImage, detections);
        result1.Should().NotBeNullOrEmpty();

        // Test with large image
        var largeImage = CreateTestImage(1920, 1080);
        detections[0].X = 500;
        detections[0].Y = 300;
        detections[0].Width = 200;
        detections[0].Height = 400;
        var result2 = await _service.AnnotateImageAsync(largeImage, detections);
        result2.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task AnnotateImageAsync_ResultCanBeDecoded()
    {
        // Arrange
        var imageBytes = CreateTestImage(640, 480);
        var detections = new List<BoundingBox>
        {
            new BoundingBox { X = 100, Y = 150, Width = 120, Height = 280, Confidence = 0.85f, Label = "person" }
        };

        // Act
        var result = await _service.AnnotateImageAsync(imageBytes, detections);
        var decodedBytes = Convert.FromBase64String(result);

        // Assert
        using var image = Image.Load(decodedBytes);
        image.Should().NotBeNull();
        image.Width.Should().Be(640);
        image.Height.Should().Be(480);
    }

    private byte[] CreateTestImage(int width, int height)
    {
        using var image = new Image<Rgb24>(width, height);
        
        // Fill with a simple pattern
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                image[x, y] = new Rgb24((byte)(x % 256), (byte)(y % 256), 128);
            }
        }

        using var memoryStream = new MemoryStream();
        image.SaveAsJpeg(memoryStream);
        return memoryStream.ToArray();
    }

    private bool IsValidBase64(string base64)
    {
        try
        {
            Convert.FromBase64String(base64);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
