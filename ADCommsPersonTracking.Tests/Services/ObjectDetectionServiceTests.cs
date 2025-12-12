using ADCommsPersonTracking.Api.Models;
using ADCommsPersonTracking.Api.Services;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace ADCommsPersonTracking.Tests.Services;

public class ObjectDetectionServiceTests
{
    private readonly ObjectDetectionService _service;

    public ObjectDetectionServiceTests()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ObjectDetection:ModelPath"] = "models/nonexistent.onnx"
            })
            .Build();

        var logger = Mock.Of<ILogger<ObjectDetectionService>>();
        _service = new ObjectDetectionService(configuration, logger);
    }

    [Fact]
    public async Task DetectPersonsAsync_WithoutModel_ThrowsInvalidOperationException()
    {
        // Arrange
        var imageBytes = CreateTestImage(640, 480);

        // Act
        var act = async () => await _service.DetectPersonsAsync(imageBytes);

        // Assert
        // When model is not available, service throws exception
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Inference session is not initialized*")
            .WithMessage("*models/nonexistent.onnx*");
    }

    [Fact]
    public async Task DetectPersonsAsync_WithInvalidImageBytes_ThrowsException()
    {
        // Arrange
        var invalidBytes = new byte[] { 1, 2, 3, 4, 5 };

        // Act
        var act = async () => await _service.DetectPersonsAsync(invalidBytes);

        // Assert - When model is not available, it throws InvalidOperationException
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Inference session is not initialized*");
    }

    [Fact]
    public void CalculateIoU_WithIdenticalBoxes_ReturnsOne()
    {
        // Arrange
        var box1 = new BoundingBox { X = 100, Y = 100, Width = 50, Height = 50, Confidence = 0.9f, Label = "person" };
        var box2 = new BoundingBox { X = 100, Y = 100, Width = 50, Height = 50, Confidence = 0.8f, Label = "person" };

        // Act
        var iou = CalculateIoU(box1, box2);

        // Assert
        iou.Should().BeApproximately(1.0f, 0.01f);
    }

    [Fact]
    public void CalculateIoU_WithNoOverlap_ReturnsZero()
    {
        // Arrange
        var box1 = new BoundingBox { X = 0, Y = 0, Width = 50, Height = 50, Confidence = 0.9f, Label = "person" };
        var box2 = new BoundingBox { X = 100, Y = 100, Width = 50, Height = 50, Confidence = 0.8f, Label = "person" };

        // Act
        var iou = CalculateIoU(box1, box2);

        // Assert
        iou.Should().Be(0.0f);
    }

    [Fact]
    public void CalculateIoU_WithPartialOverlap_ReturnsCorrectValue()
    {
        // Arrange - boxes overlap by 25x25 pixels
        var box1 = new BoundingBox { X = 0, Y = 0, Width = 50, Height = 50, Confidence = 0.9f, Label = "person" };
        var box2 = new BoundingBox { X = 25, Y = 25, Width = 50, Height = 50, Confidence = 0.8f, Label = "person" };

        // Act
        var iou = CalculateIoU(box1, box2);

        // Assert
        // Intersection: 25*25 = 625
        // Union: 50*50 + 50*50 - 625 = 4375
        // IoU: 625/4375 ≈ 0.143
        iou.Should().BeApproximately(0.143f, 0.01f);
    }

    [Fact]
    public void ApplyNMS_WithOverlappingBoxes_RemovesDuplicates()
    {
        // Arrange
        var boxes = new List<BoundingBox>
        {
            new BoundingBox { X = 100, Y = 100, Width = 50, Height = 50, Confidence = 0.9f, Label = "person" },
            new BoundingBox { X = 105, Y = 105, Width = 50, Height = 50, Confidence = 0.8f, Label = "person" }, // High overlap
            new BoundingBox { X = 300, Y = 300, Width = 50, Height = 50, Confidence = 0.85f, Label = "person" }  // No overlap
        };

        // Act
        var result = ApplyNMS(boxes, 0.5f);

        // Assert
        result.Should().HaveCount(2); // Should keep the highest confidence from overlapping pair + the non-overlapping one
        result.Should().Contain(b => b.Confidence == 0.9f); // Highest confidence from overlapping pair
        result.Should().Contain(b => b.Confidence == 0.85f); // Non-overlapping box
    }

    [Fact]
    public void ApplyNMS_WithNoOverlap_KeepsAllBoxes()
    {
        // Arrange
        var boxes = new List<BoundingBox>
        {
            new BoundingBox { X = 0, Y = 0, Width = 50, Height = 50, Confidence = 0.9f, Label = "person" },
            new BoundingBox { X = 100, Y = 100, Width = 50, Height = 50, Confidence = 0.8f, Label = "person" },
            new BoundingBox { X = 200, Y = 200, Width = 50, Height = 50, Confidence = 0.85f, Label = "person" }
        };

        // Act
        var result = ApplyNMS(boxes, 0.5f);

        // Assert
        result.Should().HaveCount(3);
    }

    private byte[] CreateTestImage(int width, int height)
    {
        using var image = new Image<Rgb24>(width, height);
        
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

    // Helper methods to test NMS and IoU algorithms
    private float CalculateIoU(BoundingBox box1, BoundingBox box2)
    {
        var x1 = Math.Max(box1.X, box2.X);
        var y1 = Math.Max(box1.Y, box2.Y);
        var x2 = Math.Min(box1.X + box1.Width, box2.X + box2.Width);
        var y2 = Math.Min(box1.Y + box1.Height, box2.Y + box2.Height);

        var intersectionArea = Math.Max(0, x2 - x1) * Math.Max(0, y2 - y1);
        var box1Area = box1.Width * box1.Height;
        var box2Area = box2.Width * box2.Height;
        var unionArea = box1Area + box2Area - intersectionArea;

        return unionArea > 0 ? intersectionArea / unionArea : 0;
    }

    private List<BoundingBox> ApplyNMS(List<BoundingBox> boxes, float iouThreshold)
    {
        var result = new List<BoundingBox>();
        var sortedBoxes = boxes.OrderByDescending(b => b.Confidence).ToList();

        while (sortedBoxes.Any())
        {
            var current = sortedBoxes.First();
            result.Add(current);
            sortedBoxes.RemoveAt(0);

            sortedBoxes = sortedBoxes.Where(box => 
                CalculateIoU(current, box) < iouThreshold).ToList();
        }

        return result;
    }
}
